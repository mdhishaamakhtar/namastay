module Namastay.Domain.Trip.Routing

open Namastay.Domain.Catalog
open Namastay.Shared.Contracts

type RoutedStop = {
    Place: Place
    Days: int
    Arrival: TransportLink option
}

let private regionLimit days =
    if days <= 7 then 1
    elif days <= 14 then 2
    else 3

let private allowed (request: TripRequest) (link: TransportLink) =
    request.TransportComfort = TransportComfort.Anything || link.Mode <> Mode.Flight

let private fastestLink (links: TransportLink list) (request: TripRequest) (from: string) (target: string) =
    links
    |> List.filter (fun link -> link.FromSlug = from && link.ToSlug = target && allowed request link)
    |> List.sortBy (fun link -> link.MinutesMin, link.Slug)
    |> List.tryHead

let private regionOf (catalog: Catalog) (place: Place) =
    catalog.Clusters
    |> List.tryFind (fun cluster -> cluster.Slug = place.ClusterSlug)
    |> Option.map _.RegionSlug
    |> Option.defaultValue place.ClusterSlug

/// Regions ranked by their three best places.
let private rankedRegions catalog (scored: (Place * int) list) =
    scored
    |> List.groupBy (fun (place, _) -> regionOf catalog place)
    |> List.map (fun (region, places) ->
        let strength =
            places |> List.map snd |> List.sortDescending |> List.truncate 3 |> List.sum

        region, strength)
    |> List.sortBy (fun (region, strength) -> -strength, region)
    |> List.map fst

/// Walks one region, adding the best places that connect to the route so far.
[<TailCall>]
let rec private walk (catalog: Catalog) request share stopDays (stops: RoutedStop list) used remaining =
    let last = stops |> List.tryLast

    let next =
        remaining
        |> List.tryPick (fun ((place: Place), _) ->
            match last with
            | None -> Some(place, None)
            | Some stop ->
                fastestLink catalog.Links request stop.Place.Slug place.Slug
                |> Option.map (fun link -> place, Some link))

    match next with
    | Some(place, link) when used + stopDays place <= share || used = 0 ->
        let days = stopDays place

        let stop = {
            Place = place
            Days = days
            Arrival = link
        }

        let others =
            remaining |> List.filter (fun (other: Place, _) -> other.Slug <> place.Slug)

        walk catalog request share stopDays (stops @ [ stop ]) (used + days) others
    | _ -> stops

let private walkRegion catalog request share prefersDepth (route: RoutedStop list) (candidates: (Place * int) list) =
    let stopDays (place: Place) =
        if prefersDepth then place.DaysMax else place.DaysMin

    walk catalog request share stopDays route 0 candidates

/// Shares spare days around the stops, then past their usual maximum if it must.
[<TailCall>]
let rec private fill remaining cursor (stops: RoutedStop list) =
    if remaining = 0 then
        stops
    else
        let count = stops.Length

        let target =
            [ for offset in 0 .. count - 1 -> (cursor + offset) % count ]
            |> List.tryFind (fun index -> stops[index].Days < stops[index].Place.DaysMax)
            |> Option.defaultValue (cursor % count)

        let grown =
            stops
            |> List.mapi (fun index stop ->
                if index = target then
                    { stop with Days = stop.Days + 1 }
                else
                    stop)

        fill (remaining - 1) (target + 1) grown

[<TailCall>]
let rec private trim excess (stops: RoutedStop list) =
    if excess <= 0 then
        stops
    else
        match List.rev stops with
        | last :: earlier when last.Days > excess -> List.rev ({ last with Days = last.Days - excess } :: earlier)
        | last :: earlier when not earlier.IsEmpty -> trim (excess - last.Days) (List.rev earlier)
        | _ -> stops

let private reversed catalog request (stops: RoutedStop list) =
    let places = stops |> List.rev |> List.map _.Place

    let arrivals =
        places
        |> List.pairwise
        |> List.map (fun (from, target) -> fastestLink catalog.Links request from.Slug target.Slug)

    if arrivals |> List.forall Option.isSome then
        let days = stops |> List.rev |> List.map _.Days

        Some(
            List.zip3 places days (None :: arrivals)
            |> List.map (fun (place, days, arrival) -> {
                Place = place
                Days = days
                Arrival = arrival
            })
        )
    else
        None

/// Flips the route when only its other end connects to the next region.
let private facing catalog request (route: RoutedStop list) (candidates: (Place * int) list) =
    let reaches (stops: RoutedStop list) =
        stops
        |> List.tryLast
        |> Option.exists (fun last ->
            candidates
            |> List.exists (fun (place, _) -> (fastestLink catalog.Links request last.Place.Slug place.Slug).IsSome))

    if route.IsEmpty || reaches route then
        route
    else
        match reversed catalog request route with
        | Some flipped when reaches flipped -> flipped
        | _ -> route

/// Chooses connected stops across the allowed regions, with days adding up exactly.
let route (catalog: Catalog) (request: TripRequest) prefersDepth (scored: (Place * int) list) : RoutedStop list =
    let regions =
        rankedRegions catalog scored |> List.truncate (regionLimit request.Days)

    let share = request.Days / regions.Length

    let stops =
        regions
        |> List.fold
            (fun route region ->
                let candidates =
                    scored |> List.filter (fun (place, _) -> regionOf catalog place = region)

                walkRegion catalog request share prefersDepth (facing catalog request route candidates) candidates)
            []

    let planned = stops |> List.sumBy _.Days

    if planned > request.Days then
        trim (planned - request.Days) stops
    else
        fill (request.Days - planned) 0 stops
