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

let private setting (name: string) (fallback: string) =
    match Environment.GetEnvironmentVariable name with
    | null
    | "" -> fallback
    | value -> value

let private syncMedia (folder: string) =
    let store =
        MediaStore.create {
            Endpoint = setting "MEDIA_ENDPOINT" "http://localhost:9000"
            Bucket = setting "MEDIA_BUCKET" "namastay-media"
            AccessKey = setting "MEDIA_ACCESS_KEY" "namastay"
            SecretKey = setting "MEDIA_SECRET_KEY" "namastay-local"
        }

    let sent = (MediaStore.sync store folder).GetAwaiter().GetResult()
    printfn "Uploaded %d file(s)." sent
    0

[<EntryPoint>]
let main args =
    match List.ofArray args with
    | [ "--dry-run"; folder ] -> import Run.DryRun folder
    | [ "--media"; folder ] -> syncMedia folder
    | [ folder ] -> import Run.Apply folder
    | _ ->
        eprintfn "Usage: Namastay.Importer [--dry-run | --media] <folder>"
        2
