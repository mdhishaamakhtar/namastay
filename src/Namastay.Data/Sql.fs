module Namastay.Data.Sql

open System
open Npgsql
open Namastay.Domain.Catalog

let arg (name: string) (value: 'T) = name, box value

let opt (name: string) (value: 'T option) =
    name,
    (match value with
     | Some inner -> box inner
     | None -> box DBNull.Value)

let private command (transaction: NpgsqlTransaction) (sql: string) (args: (string * obj) list) =
    let command = new NpgsqlCommand(sql, transaction.Connection, transaction)

    for name, value in args do
        command.Parameters.AddWithValue(name, value) |> ignore

    command

let exec transaction sql args =
    use prepared = command transaction sql args
    prepared.ExecuteNonQuery()

let scalar transaction sql args =
    use prepared = command transaction sql args
    prepared.ExecuteScalar()

/// Replaces the sources recorded for one row of a table.
let linkSources transaction (table: string) (slug: string) (evidence: Evidence) =
    exec transaction "delete from fact_sources where table_name = @table and row_slug = @slug" [
        arg "table" table
        arg "slug" slug
    ]
    |> ignore

    for sourceId in evidence.SourceIds |> List.distinct do
        exec
            transaction
            "insert into fact_sources (table_name, row_slug, field, source_id) values (@table, @slug, '*', @source)"
            [ arg "table" table; arg "slug" slug; arg "source" sourceId ]
        |> ignore
