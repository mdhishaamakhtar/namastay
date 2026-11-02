module Namastay.Domain.Trip.Editing

open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Domain.Trip.Routing
open Namastay.Domain.Trip.Rules
open Namastay.Shared.Contracts

/// Each known stop with its leg from the stop before. A custom stop breaks the chain.
let private route (catalog: Catalog) stops =
    stops
    |> List.mapFold
        (fun (previous: Place option) stop ->
            match stop with
            | Known(place, days) ->
                let arrival =
                    previous
                    |> Option.bind (fun from -> linkBetween catalog.Links from.Slug place.Slug)

                Some {
                    Place = place
                    Days = days
                    Arrival = arrival
                },
                Some place
            | Unchecked _ -> None, None)
        None
    |> fst

let private colours (catalog: Catalog) (original: Trip) family =
    catalog.Families
    |> List.tryFind (fun item -> item.Slug = family)
    |> Option.map (fun item -> {
        Deep = item.Deep
        Mid = item.Mid
        Pale = item.Pale
        TextOnDeep = item.TextOnDeep
    })
    |> Option.defaultValue original.Stops.Head.Colours

let private customStop (catalog: Catalog) (original: Trip) family index name days : TripStop = {
    PlaceSlug = $"custom-{index + 1}"
    Name = name
    Days = days
    FamilySlug = family
    Colours = colours catalog original family
    Access = ContractMapping.access Gate.Open
    StayArea = None
    Photo = None
    Plans = [ for day in 1..days -> { Day = day; Plan = Builder.spareDay } ]
    Food = None
    Arrival = None
    Retired = false
    Custom = true
}

let private familyDays (stop: RoutedStop) : Builder.FamilyDays = {
    Slug = stop.Place.Slug
    Name = stop.Place.Name
    Days = stop.Days
    Family = stop.Place.FamilySlug
}

let private leadingFamily (original: Trip) (routed: RoutedStop list) =
    match routed with
    | [] -> original.BaseFamily
    | _ -> routed |> List.map familyDays |> Builder.baseFamily []

let private rebuild (catalog: Catalog) (original: Trip) (edit: TripEdit) (review: Review) : Trip =
    let legs = route catalog review.Stops
    let routed = legs |> List.choose id
    let family = leadingFamily original routed
    let diet = original.Diet |> Option.defaultValue Diet.Everything

    let stops =
        List.zip review.Stops legs
        |> List.mapi (fun index (stop, leg) ->
            match stop, leg with
            | Unchecked(name, days), _ -> customStop catalog original family index name days
            | Known _, Some known -> Builder.toStop catalog original.Budget diet known
            | Known _, None -> invalidOp "Every known stop has a leg.")

    {
        original with
            Title = edit.Title.Trim()
            Month = edit.Month
            BaseFamily = family
            Arrive = Gateways.arrive catalog routed
            Depart = Gateways.depart catalog routed
            Stops = stops
            Swaps = []
            Notices = Builder.notices catalog edit.Month routed
            BestMonths = if routed.IsEmpty then [] else Builder.bestMonths routed
    }

/// Applies a traveller's changes to their copy of a trip, or says why they can't be saved.
let apply (catalog: Catalog) (original: Trip) (edit: TripEdit) : Result<EditedTrip, string list> =
    let review = Rules.check catalog edit

    match review.Blocked with
    | [] ->
        Ok {
            Trip = rebuild catalog original edit review
            Warnings = review.Warnings
        }
    | reasons -> Error reasons
