module Namastay.Domain.Trip.Routing

open Namastay.Domain.Catalog
open Namastay.Shared.Contracts

type RoutedStop = {
    Place: Place
    Days: int
    Arrival: TransportLink option
}

let private beamWidth = 60
let private unfilledDayPenalty = 10
let private oneLinkCost = 25
let private farCost = 70

let private regionLimit days =
    if days <= 7 then 1
    elif days <= 14 then 2
    else 3

let private allowed (request: TripRequest) (link: TransportLink) =
    request.TransportComfort = TransportComfort.Anything || link.Mode <> Mode.Flight

/// The quickest link by any mode, for routes a traveller chose themselves.
let linkBetween (links: TransportLink list) (from: string) (target: string) =
    links
    |> List.filter (fun link -> link.FromSlug = from && link.ToSlug = target)
    |> List.sortBy (fun link -> link.MinutesMin, link.Slug)
    |> List.tryHead

let private regionOf (catalog: Catalog) (place: Place) =
    catalog.Clusters
    |> List.tryFind (fun cluster -> cluster.Slug = place.ClusterSlug)
    |> Option.map _.RegionSlug
    |> Option.defaultValue place.ClusterSlug

/// A route so far, newest stop first.
type private Path = {
    Stops: RoutedStop list
    Regions: Set<string>
    Score: int
    FirstScore: int
    Planned: int
    Minimum: int
    Capacity: int
}

let private start catalog stopDays ((place: Place), score) = {
    Stops = [
        {
            Place = place
            Days = stopDays place
            Arrival = None
        }
    ]
    Regions = Set.singleton (regionOf catalog place)
    Score = score
    FirstScore = score
    Planned = stopDays place
    Minimum = place.DaysMin
    Capacity = place.DaysMax
}

let private extend catalog (links: Map<string * string, TransportLink>) stopDays limit days scored (path: Path) =
    let last = path.Stops.Head.Place

    if path.Planned >= days then
        []
    else
        scored
        |> List.choose (fun ((place: Place), score) ->
            let regions = path.Regions.Add(regionOf catalog place)
            let used = path.Stops |> List.exists (fun stop -> stop.Place.Slug = place.Slug)

            match links.TryFind(last.Slug, place.Slug) with
            | Some link when not used && regions.Count <= limit && path.Minimum + place.DaysMin <= days ->
                Some {
                    Stops =
                        {
                            Place = place
                            Days = stopDays place
                            Arrival = Some link
                        }
                        :: path.Stops
                    Regions = regions
                    Score = path.Score + score
                    FirstScore = path.FirstScore
                    Planned = path.Planned + stopDays place
                    Minimum = path.Minimum + place.DaysMin
                    Capacity = path.Capacity + place.DaysMax
                }
            | _ -> None)

/// Every route the search reached, keeping only the strongest at each length.
[<TailCall>]
let rec private explore grow reached frontier =
    match frontier with
    | [] -> reached
    | _ ->
        let next =
            frontier
            |> List.collect grow
            |> List.sortByDescending _.Score
            |> List.truncate beamWidth

        explore grow (next @ reached) next

/// What it costs a route to begin at a place: nothing in an airport city, a little
/// one link away, and a lot anywhere further. With no airports in the data, every place is free.
let private startCost (catalog: Catalog) (links: Map<string * string, TransportLink>) =
    let airports = catalog.Places |> List.filter _.Airport.IsSome |> List.map _.Slug

    fun (slug: string) ->
        if airports.IsEmpty || List.contains slug airports then
            0
        elif airports |> List.exists (fun airport -> links.ContainsKey(airport, slug)) then
            oneLinkCost
        else
            farCost

/// The route's taste, less penalties for days it can't honestly fill and for ends nobody can fly to.
let private worth days cost (path: Path) =
    path.Score
    - unfilledDayPenalty * max 0 (days - path.Capacity)
    - cost (List.last path.Stops).Place.Slug
    - cost path.Stops.Head.Place.Slug / 2

let private bestPath days cost (paths: Path list) =
    paths
    |> List.minBy (fun path ->
        let slugs = path.Stops |> List.map _.Place.Slug
        -(worth days cost path), -path.FirstScore, slugs)

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

/// Takes spare days from the latest stops. A stop goes only when none has a day to spare.
[<TailCall>]
let rec private trim excess (stops: RoutedStop list) =
    let spare =
        stops |> List.tryFindIndexBack (fun stop -> stop.Days > stop.Place.DaysMin)

    match stops, spare with
    | _ when excess <= 0 -> stops
    | _, Some index ->
        let shorter =
            stops
            |> List.mapi (fun at stop ->
                if at = index then
                    { stop with Days = stop.Days - 1 }
                else
                    stop)

        trim (excess - 1) shorter
    | [ only ], None when only.Days > excess -> [ { only with Days = only.Days - excess } ]
    | _ :: _ :: _, None ->
        let last = List.last stops
        trim (excess - last.Days) (List.take (stops.Length - 1) stops)
    | _ -> stops

let private planned (stops: RoutedStop list) = stops |> List.sumBy _.Days

/// Chooses connected stops within the allowed regions, with days adding up exactly.
let route (catalog: Catalog) (request: TripRequest) prefersDepth (scored: (Place * int) list) : RoutedStop list =
    let stopDays (place: Place) =
        if prefersDepth then
            place.DaysMax
        else
            (place.DaysMin + place.DaysMax + 1) / 2

    let links =
        catalog.Links
        |> List.filter (allowed request)
        |> List.sortBy (fun link -> link.MinutesMin, link.Slug)
        |> List.rev
        |> List.map (fun link -> (link.FromSlug, link.ToSlug), link)
        |> Map.ofList

    let grow =
        extend catalog links stopDays (regionLimit request.Days) request.Days scored

    let starts = scored |> List.map (start catalog stopDays)

    let stops =
        explore grow starts starts
        |> bestPath request.Days (startCost catalog links)
        |> _.Stops
        |> List.rev

    let trimmed = trim (planned stops - request.Days) stops
    fill (request.Days - planned trimmed) 0 trimmed
