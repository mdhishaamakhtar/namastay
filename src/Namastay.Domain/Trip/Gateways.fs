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

let plainName (place: Place) = PlaceName.plain place.Name

let private gateway (catalog: Catalog) hop (place: Place) =
    Airports.nearest catalog hop place
    |> Option.map (fun way -> {
        City = plainName way.Airport
        Airport = way.Airport.Airport |> Option.defaultValue ""
        Via = way.Via |> Option.map plainName
        Leg = way.Leg |> Option.map toLeg
    })

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
