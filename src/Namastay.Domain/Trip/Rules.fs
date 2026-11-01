module Namastay.Domain.Trip.Rules

open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Shared.Contracts

type EditedStop =
    | Known of Place * days: int
    | Unchecked of name: string * days: int

/// Blocked reasons stop a save. Warnings go back to the traveller, who decides.
type Review = {
    Stops: EditedStop list
    Blocked: string list
    Warnings: string list
}

let private maxTitle = 120
let private maxName = 80

let private days stop =
    match stop with
    | Known(_, days)
    | Unchecked(_, days) -> days

let private resolveKnown (catalog: Catalog) (slug: string) requested =
    let excluded = catalog.Exclusions |> List.tryFind (fun item -> item.Slug = slug)

    let place =
        catalog.Places
        |> List.tryFind (fun place -> place.Slug = slug && place.Published)

    match excluded, place with
    | Some item, _ -> Error $"We can't add {item.Name}. {item.Reason}"
    | None, Some found -> Ok(Known(found, requested))
    | None, None -> Error $"We don't know a place called {slug}."

let private resolveCustom (name: string) requested =
    let trimmed = name.Trim()

    if trimmed = "" || trimmed.Length > maxName then
        Error $"Please give each of your own stops a name of up to {maxName} characters."
    else
        Ok(Unchecked(trimmed, requested))

let private resolve catalog (stop: StopEdit) =
    match stop.PlaceSlug, stop.CustomName with
    | Some slug, _ -> resolveKnown catalog slug stop.Days
    | None, Some name -> resolveCustom name stop.Days
    | None, None -> resolveCustom "" stop.Days

let private brokenLegs (catalog: Catalog) stops =
    stops
    |> List.pairwise
    |> List.choose (fun pair ->
        match pair with
        | Known(from, _), Known(target, _) when (Routing.linkBetween catalog.Links from.Slug target.Slug).IsNone ->
            Some $"We have no route from {from.Name} to {target.Name}."
        | _ -> None)

let private shapeProblems (edit: TripEdit) = [
    let total = edit.Stops |> List.sumBy _.Days
    let title = edit.Title.Trim()

    if title = "" || title.Length > maxTitle then
        $"Please give your trip a name of up to {maxTitle} characters."

    if edit.Month |> Option.exists (fun month -> month < 1 || month > 12) then
        "Please choose a month from January to December."

    if total < RequestRules.minDays || total > RequestRules.maxDays then
        $"A trip needs {RequestRules.minDays} to {RequestRules.maxDays} days."

    if edit.Stops |> List.exists (fun stop -> stop.Days < 1) then
        "Every stop needs at least one day."
]

let private warning month stop =
    match stop, month with
    | Known(place, _), Some chosen when Scoring.monthRating chosen place = Rating.Avoid ->
        Some $"{place.Name} isn't a good choice in {Builder.monthName chosen}."
    | Known _, _ -> None
    | Unchecked(name, _), _ -> Some $"We haven't checked {name}, so getting there is yours to plan."

let check (catalog: Catalog) (edit: TripEdit) : Review =
    let resolved = edit.Stops |> List.map (resolve catalog)

    let stops =
        resolved
        |> List.choose (function
            | Ok stop -> Some stop
            | Error _ -> None)

    let unknown =
        resolved
        |> List.choose (function
            | Error reason -> Some reason
            | Ok _ -> None)

    {
        Stops = stops
        Blocked = shapeProblems edit @ unknown @ brokenLegs catalog stops
        Warnings = stops |> List.choose (warning edit.Month)
    }
