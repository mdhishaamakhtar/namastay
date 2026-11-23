/// How a stop reaches the cities foreign visitors fly into.
module Namastay.Domain.Trip.Airports

open Namastay.Domain.Catalog

/// How much further a traveller will go for a main hub, which has far more long-haul flights.
let hubPreference = 150

/// The way between a stop and an airport city: direct, or through one city between.
type Way = {
    Airport: Place
    Via: Place option
    /// The leg that touches the airport city. None when the stop is the airport city.
    Leg: TransportLink option
    /// The whole journey, both parts when there are two.
    Minutes: int
}

let private wayTo (catalog: Catalog) (hop: string -> string -> TransportLink option) (place: Place) (airport: Place) =
    let direct =
        hop place.Slug airport.Slug
        |> Option.map (fun link -> {
            Airport = airport
            Via = None
            Leg = Some link
            Minutes = link.MinutesMin
        })

    let through (middle: Place) =
        match hop place.Slug middle.Slug, hop middle.Slug airport.Slug with
        | Some first, Some second ->
            Some {
                Airport = airport
                Via = Some middle
                Leg = Some second
                Minutes = first.MinutesMin + second.MinutesMin
            }
        | _ -> None

    direct
    |> Option.orElse (
        catalog.Places
        |> List.filter _.Published
        |> List.choose through
        |> List.sortBy _.Minutes
        |> List.tryHead
    )

/// The airport city a stop uses: itself if it has one, else the nearest, with a direct
/// way before a way through a city, and a main hub before a smaller airport a little closer.
/// `hop` takes the near end first, so it works for the journey in and the journey out.
let nearest (catalog: Catalog) (hop: string -> string -> TransportLink option) (place: Place) : Way option =
    if place.Airport.IsSome then
        Some {
            Airport = place
            Via = None
            Leg = None
            Minutes = 0
        }
    else
        catalog.Places
        |> List.filter _.Airport.IsSome
        |> List.choose (wayTo catalog hop place)
        |> List.sortBy (fun way ->
            way.Via.IsSome,
            (if way.Airport.Hub then
                 way.Minutes
             else
                 way.Minutes + hubPreference))
        |> List.tryHead
