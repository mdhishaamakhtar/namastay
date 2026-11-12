/// Prints a scorecard, keeps one as a file, and shows how a run moved against it.
module Namastay.Bench.Report

open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open Namastay.Bench.Metrics
open Namastay.Bench.Session
open Namastay.Shared.Contracts

let private options =
    let settings =
        JsonFSharpOptions.Default().WithUnionUnwrapFieldlessTags().ToJsonSerializerOptions()

    settings.WriteIndented <- true
    settings

/// A change this small is noise, not a move.
let private noise = 0.05

let save (path: string) (lines: Line list) =
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath path))
    |> ignore

    File.WriteAllText(path, JsonSerializer.Serialize(lines, options))

let load (path: string) : Line list =
    JsonSerializer.Deserialize<Line list>(File.ReadAllText path, options)

let private wants (better: Better) =
    match better with
    | Better.Higher -> "higher"
    | Better.Lower -> "lower"
    | Better.Zero -> "zero"

/// Whether a line moved the way it should.
let verdict (line: Line) (before: float) =
    let delta = line.Value - before

    match line.Better with
    | _ when abs delta < noise -> "same"
    | Better.Higher -> if delta > 0.0 then "better" else "worse"
    | Better.Lower
    | Better.Zero -> if delta < 0.0 then "better" else "worse"

let private row (against: Line list option) (line: Line) =
    let before =
        against
        |> Option.bind (List.tryFind (fun other -> other.Group = line.Group && other.Name = line.Name))

    let change =
        match before with
        | Some found -> sprintf "%10.1f %+8.1f  %s" found.Value (line.Value - found.Value) (verdict line found.Value)
        | None -> ""

    sprintf "%-10s %-28s %8.1f  %-7s%s" line.Group line.Name line.Value (wants line.Better) change

let print (against: Line list option) (lines: Line list) =
    let heading =
        sprintf "%-10s %-28s %8s  %-7s" "group" "line" "value" "wants"
        + (if against.IsSome then
               sprintf "%10s %8s" "before" "change"
           else
               "")

    printfn "%s" heading

    for line in lines do
        printfn "%s" (row against line)

let private gateway (label: string) (found: TripGateway option) =
    match found with
    | Some city ->
        let via =
            city.Via
            |> Option.map (fun middle -> $" via {middle}")
            |> Option.defaultValue ""

        printfn "  %s %s%s" label city.City via
    | None -> ()

/// One traveller's session in full, to see what a line on the scorecard means.
let session (found: Session) =
    let persona = found.Persona
    let month = persona.Month |> Option.map string |> Option.defaultValue "any month"

    let scope =
        match persona.Feelings with
        | [] -> ""
        | feelings -> sprintf " (planned from %s)" (String.concat " + " feelings)

    printfn
        "%s: %s%s, %d days, %s, %A, %A"
        persona.Name
        (String.concat " + " persona.Interests)
        scope
        persona.Days
        month
        persona.Travellers
        persona.Budget

    let liked = found.Answers |> List.filter _.Liked |> List.map _.Card
    printfn "  liked %s" (String.concat ", " liked)

    match found.Early with
    | Some finish -> printfn "  settled after %d of %d cards" finish.Cards found.Answers.Length
    | None -> printfn "  never settled in %d cards" found.Answers.Length

    match found.Trip with
    | Error error -> printfn "  no trip: %A" error
    | Ok trip ->
        gateway "land in" trip.Arrive

        for stop in trip.Stops do
            let leg =
                stop.Arrival
                |> Option.map (fun leg -> $" ({leg.Mode} {leg.MinutesMin / 60}h{leg.MinutesMin % 60:D2})")
                |> Option.defaultValue ""

            let reason =
                stop.Reason |> Option.map (fun why -> $". {why}") |> Option.defaultValue ""

            printfn "  %s, %d days%s%s" stop.Name stop.Days leg reason

        gateway "fly home from" trip.Depart
