open System
open System.IO
open System.Security.Cryptography
open Namastay.Data
open Namastay.Data.Import
open Namastay.Data.Media

let private localDatabase =
    "Host=localhost;Port=5433;Database=namastay;Username=namastay;Password=namastay"

let private connectionString () =
    match Environment.GetEnvironmentVariable "NAMASTAY_DB" with
    | null
    | "" -> localDatabase
    | value -> value

let private folderHash (folder: string) =
    let bytes =
        Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories)
        |> Array.sort
        |> Array.collect File.ReadAllBytes

    Convert.ToHexString(SHA256.HashData bytes)

let private fail (errors: string list) =
    errors |> List.iter (eprintfn "%s")
    eprintfn "%d problem(s). Nothing was changed." errors.Length
    1

let private import mode (folder: string) =
    let database = connectionString ()

    let result =
        Migrate.run database
        |> Result.mapError List.singleton
        |> Result.bind (fun () -> Load.fromFolder folder)
        |> Result.bind (Run.run database mode (folderHash folder))

    match result with
    | Ok counts ->
        counts |> Map.iter (printfn "%-14s %d")

        printfn
            "%s"
            (if mode = Run.DryRun then
                 "Dry run: nothing was written."
             else
                 "Imported.")

        0
    | Error errors -> fail errors

let private keysShown = 8

let private keys (label: string) (found: string list) =
    match found with
    | [] -> ()
    | _ ->
        let more =
            if found.Length > keysShown then
                $" and {found.Length - keysShown} more"
            else
                ""

        printfn "  %s: %s%s" label (found |> List.truncate keysShown |> String.concat ", ") more

let private report mode (changes: Publish.TableChange list) =
    for change in changes do
        printfn
            "%-16s %d new, %d changed, %d removed"
            change.Table
            change.Added.Length
            change.Changed.Length
            change.Removed.Length

        keys "new" change.Added
        keys "changed" change.Changed
        keys "removed" change.Removed

    match mode, changes with
    | _, [] -> printfn "Nothing to change. Production already matches the local database."
    | Run.DryRun, _ -> printfn "Dry run: nothing was written."
    | Run.Apply, _ -> printfn "Published."

/// A dry run writes nothing, so it can't bring production's tables up to date to compare them.
let private ready mode (target: string) =
    match mode with
    | Run.Apply -> Migrate.run target |> Result.mapError List.singleton
    | Run.DryRun when Migrate.isBehind target ->
        Error [
            "production is behind on migrations, so a dry run can't compare yet. Publishing runs them first."
        ]
    | Run.DryRun -> Ok()

/// Sends the local database's content to the database named by PUBLISH_DB.
let private publish mode =
    match Environment.GetEnvironmentVariable "PUBLISH_DB" with
    | null
    | "" -> fail [ "set PUBLISH_DB to the database to publish to." ]
    | target ->
        match
            ready mode target
            |> Result.bind (fun () -> Publish.run (connectionString ()) target mode)
        with
        | Ok changes ->
            report mode changes
            0
        | Error errors -> fail errors

let private setting (name: string) (fallback: string) =
    match Environment.GetEnvironmentVariable name with
    | null
    | "" -> fallback
    | value -> value

let private mediaStore () =
    MediaStore.create {
        Endpoint = setting "MEDIA_ENDPOINT" "http://localhost:9000"
        Bucket = setting "MEDIA_BUCKET" "namastay-media"
        AccessKey = setting "MEDIA_ACCESS_KEY" "namastay"
        SecretKey = setting "MEDIA_SECRET_KEY" "namastay-local"
        PathStyle = setting "MEDIA_PATH_STYLE" "true" = "true"
    }

let private syncMedia (folder: string) =
    let sent = (MediaStore.sync (mediaStore ()) folder).GetAwaiter().GetResult()
    printfn "Uploaded %d file(s)." sent
    0

let private pruneMedia (folder: string) (backup: string) =
    let removed =
        (MediaStore.prune (mediaStore ()) folder backup).GetAwaiter().GetResult()

    printfn "Removed %d file(s), with copies in %s." removed backup
    0

[<EntryPoint>]
let main args =
    match List.ofArray args with
    | [ "--dry-run"; folder ] -> import Run.DryRun folder
    | [ "--publish" ] -> publish Run.Apply
    | [ "--publish-check" ] -> publish Run.DryRun
    | [ "--media"; folder ] -> syncMedia folder
    | [ "--media-prune"; folder; backup ] -> pruneMedia folder backup
    | [ folder ] -> import Run.Apply folder
    | _ ->
        eprintfn "Usage: Namastay.Importer [--dry-run | --media | --media-prune] <folder> [backup]"
        eprintfn "       Namastay.Importer --publish | --publish-check"
        2
