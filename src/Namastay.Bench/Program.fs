module Namastay.Bench.Program

open Namastay.Data.Import
open Namastay.Domain.Catalog

type private Options = {
    Folder: string
    Save: string option
    Against: string option
    Show: int
}

/// The same travellers every run.
let private seed = 2026

[<TailCall>]
let rec private parse options args =
    match args with
    | "--save" :: path :: rest -> parse { options with Save = Some path } rest
    | "--against" :: path :: rest -> parse { options with Against = Some path } rest
    | "--show" :: count :: rest -> parse { options with Show = int count } rest
    | folder :: rest when not (folder.StartsWith "--") -> parse { options with Folder = folder } rest
    | [] -> Some options
    | _ -> None

let private run options =
    match Load.fromFolder options.Folder with
    | Error errors ->
        errors |> List.iter (eprintfn "%s")
        1
    | Ok catalog ->
        let graph = ContractMapping.journeyGraph catalog

        let sessions =
            Personas.set catalog seed
            |> List.toArray
            |> Array.Parallel.map (Session.play catalog graph)
            |> Array.toList

        let lines = Metrics.scorecard catalog sessions
        Report.print (options.Against |> Option.map Report.load) lines
        options.Save |> Option.iter (fun path -> Report.save path lines)
        sessions |> List.truncate options.Show |> List.iter Report.session
        0

[<EntryPoint>]
let main args =
    let defaults = {
        Folder = "data"
        Save = None
        Against = None
        Show = 0
    }

    match parse defaults (List.ofArray args) with
    | Some options -> run options
    | None ->
        eprintfn "Usage: Namastay.Bench [folder] [--save <file>] [--against <file>] [--show <sessions>]"
        2
