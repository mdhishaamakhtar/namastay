module Namastay.Data.Import.Run

open System
open System.Text.Json
open Npgsql
open Namastay.Data.Sql
open Namastay.Domain.Catalog

type Mode =
    | DryRun
    | Apply

type ImportCounts = Map<string, int>

let private countsOf (catalog: Catalog) : ImportCounts =
    Map.ofList [
        "regions", catalog.Regions.Length
        "clusters", catalog.Clusters.Length
        "families", catalog.Families.Length
        "interests", catalog.Interests.Length
        "places", catalog.Places.Length
        "links", catalog.Links.Length
        "festivals", catalog.Festivals.Length
        "notices", catalog.Notices.Length
        "exclusions", catalog.Exclusions.Length
        "sources", catalog.Sources.Length
        "illustrations", catalog.Illustrations.Length
        "photos", catalog.Photos.Length
        "cards", catalog.Cards.Length
    ]

let private recordBatch (connection: NpgsqlConnection) (hash: string) (status: string) (counts: ImportCounts) =
    use transaction = connection.BeginTransaction()

    exec
        transaction
        "insert into import_batches (started_at, finished_at, file_hash, status, counts)
         values (now(), now(), @hash, @status, @counts::jsonb)"
        [
            arg "hash" hash
            arg "status" status
            arg "counts" (JsonSerializer.Serialize counts)
        ]
    |> ignore

    transaction.Commit()

let private applyAll transaction catalog =
    Geography.apply transaction catalog
    Places.apply transaction catalog
    Transport.apply transaction catalog
    Events.apply transaction catalog
    Journey.apply transaction catalog

let private write (connection: NpgsqlConnection) mode hash catalog =
    let counts = countsOf catalog

    try
        use transaction = connection.BeginTransaction()
        applyAll transaction catalog

        match mode with
        | DryRun -> transaction.Rollback()
        | Apply ->
            transaction.Commit()
            recordBatch connection hash "committed" counts

        Ok counts
    with :? PostgresException as error ->
        if mode = Apply then
            recordBatch connection hash "failed" Map.empty

        Error [ $"database rejected the batch: {error.MessageText}" ]

/// Validates, then writes everything in one transaction or nothing at all.
let run (connectionString: string) (mode: Mode) (hash: string) (catalog: Catalog) : Result<ImportCounts, string list> =
    use connection = new NpgsqlConnection(connectionString)
    connection.Open()

    match Namastay.Domain.Catalog.Validation.check catalog with
    | [] -> write connection mode hash catalog
    | errors ->
        if mode = Apply then
            recordBatch connection hash "failed" Map.empty

        Error(errors |> List.map (fun error -> $"{error.Table}/{error.Slug}: {error.Message}"))
