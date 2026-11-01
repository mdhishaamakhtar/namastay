/// Where a trip lands and where it flies home from.
module Namastay.Domain.Trip.Gateways

open Namastay.Domain.Catalog
open Namastay.Domain.Trip.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

let toLeg (link: TransportLink) = {
    Mode = ContractMapping.travelMode link.Mode
    MinutesMin = link.MinutesMin
    MinutesMax = link.MinutesMax
    ComfortNote = link.ComfortNote
}

/// How much further a traveller will go for a main hub, which has far more long-haul flights.
let private hubPreference = 150

let private airports (catalog: Catalog) =
    catalog.Places |> List.filter _.Airport.IsSome

let plainName (place: Place) = PlaceName.plain place.Name

let private atAirport (place: Place) = {
    City = plainName place
    Airport = place.Airport |> Option.defaultValue ""
    Via = None
    Leg = None
}

/// The way between a stop and one airport city: direct, or through one city between.
/// `hop` takes the near end first, so it works for the journey in and the journey out.
let private wayTo (catalog: Catalog) (hop: string -> string -> TransportLink option) (place: Place) (airport: Place) =
    let direct =
        hop place.Slug airport.Slug
        |> Option.map (fun link ->
            link.MinutesMin,
            {
                atAirport airport with
                    Leg = Some(toLeg link)
            })

    let through (middle: Place) =
        match hop place.Slug middle.Slug, hop middle.Slug airport.Slug with
        | Some first, Some second ->
            Some(
                first.MinutesMin + second.MinutesMin,
                {
                    atAirport airport with
                        Via = Some(plainName middle)
                        Leg = Some(toLeg second)
                }
            )
        | _ -> None

    direct
    |> Option.orElse (
        catalog.Places
        |> List.filter _.Published
        |> List.choose through
        |> List.sortBy fst
        |> List.tryHead
    )

let private gateway (catalog: Catalog) hop (place: Place) =
    if place.Airport.IsSome then
        Some(atAirport place)
    else
        airports catalog
        |> List.choose (fun airport ->
            wayTo catalog hop place airport
            |> Option.map (fun (minutes, way) ->
                let reach = if airport.Hub then minutes else minutes + hubPreference
                (way.Via.IsSome, reach), way))
        |> List.sortBy fst
        |> List.tryHead
        |> Option.map snd

/// Some routes are only recorded in one direction, but the same road or flight runs back.
let private eitherWay (catalog: Catalog) from target =
    [ linkBetween catalog.Links from target; linkBetween catalog.Links target from ]
    |> List.choose id
    |> List.sortBy _.MinutesMin
    |> List.tryHead

/// Foreign visitors fly in, so a trip starts from where they land.
let arrive (catalog: Catalog) (stops: RoutedStop list) =
    let hop near far = eitherWay catalog far near

    stops
    |> List.tryHead
    |> Option.bind (fun first -> gateway catalog hop first.Place)

let depart (catalog: Catalog) (stops: RoutedStop list) =
    stops
    |> List.tryLast
    |> Option.bind (fun last -> gateway catalog (eitherWay catalog) last.Place)
