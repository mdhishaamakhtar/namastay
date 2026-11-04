module Namastay.Domain.Trip.Routing

open Namastay.Domain.Catalog
open Namastay.Shared
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
/// What an hour on the road or rails costs a route, and for someone who avoids flights
/// and so expects the long way round.
let private hourCost = 6
let private groundHourCost = 3
/// Past this, every hour counts double: a day lost to the road.
let private longLegMinutes = 480
/// Packing up and moving on costs something, so a stop has to be worth the move:
/// more for a family or someone who'd rather go deep than far, less for friends.
let private stopCost = 20
let private slowStopCost = 35
let private quickStopCost = 12

let private regionLimit days =
    if days <= 7 then 1
    elif days <= 14 then 2
    else 3

let private allowed (request: TripRequest) (link: TransportLink) =
    request.TransportComfort = TransportComfort.Anything || link.Mode <> Mode.Flight

let private travelCost (request: TripRequest) (link: TransportLink) =
    let perHour =
        match request.TransportComfort with
        | TransportComfort.Anything -> hourCost
        | TransportComfort.NoFlights -> groundHourCost

    (link.MinutesMin + max 0 (link.MinutesMin - longLegMinutes)) * perHour / 60

let private throughNote (city: Place) = $"Through {PlaceName.plain city.Name}."

/// One leg made of two, for a route that passes through a city without staying.
let private passing (middle: Place) (first: TransportLink) (second: TransportLink) = {
    second with
        MinutesMin = first.MinutesMin + second.MinutesMin
        MinutesMax = first.MinutesMax + second.MinutesMax
        ComfortNote = Some(throughNote middle)
}

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

let private start catalog stopDays worthOf (place: Place) =
    let score = worthOf [] place

    {
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

/// A main hub only starts or ends a route. A route reaches past one by passing through.
let private extend
    (catalog: Catalog)
    (request: TripRequest)
    (links: Map<string * string, TransportLink>)
    stopDays
    moveCost
    allowsRegions
    days
    (ranked: Place list)
    (worthOf: Place list -> Place -> int)
    (path: Path)
    =
    let last = path.Stops.Head.Place
    let sofar = path.Stops |> List.map _.Place

    let hubs =
        catalog.Places
        |> List.filter (fun place -> place.Hub && place.Slug <> last.Slug)

    let through (place: Place) =
        hubs
        |> List.choose (fun hub ->
            match links.TryFind(last.Slug, hub.Slug), links.TryFind(hub.Slug, place.Slug) with
            | Some first, Some second -> Some(passing hub first second)
            | _ -> None)
        |> List.sortBy _.MinutesMin
        |> List.tryHead

    let reach (place: Place) =
        links.TryFind(last.Slug, place.Slug) |> Option.orElse (through place)

    if path.Planned >= days || (last.Hub && path.Stops.Length > 1) then
        []
    else
        ranked
        |> List.choose (fun place ->
            let score = worthOf sofar place
            let regions = path.Regions.Add(regionOf catalog place)
            let used = path.Stops |> List.exists (fun stop -> stop.Place.Slug = place.Slug)

            match reach place with
            | Some link when not used && allowsRegions regions && path.Minimum + place.DaysMin <= days ->
                Some {
                    Stops =
                        {
                            Place = place
                            Days = stopDays place
                            Arrival = Some link
                        }
                        :: path.Stops
                    Regions = regions
                    Score = path.Score + score - travelCost request link - moveCost
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

/// The route's taste less its travel, less penalties for days it can't honestly fill
/// and for ends nobody can fly to.
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

/// A week or less leaves one day for the last city, a longer trip two.
let private closingDays days = if days <= 7 then 1 else 2

/// Where to stay before flying home, if the route doesn't already end somewhere people fly
/// home from: the nearest main hub that's fine in the month, else the nearest hub at all,
/// or failing that, the nearest international airport.
let private closingCity
    (catalog: Catalog)
    (request: TripRequest)
    (links: Map<string * string, TransportLink>)
    (closers: Place list)
    (stops: RoutedStop list)
    =
    let last = (List.last stops).Place

    let closes (city: Place) =
        closers |> List.exists (fun closer -> closer.Slug = city.Slug)

    let between from target =
        [ links.TryFind(from, target); links.TryFind(target, from) ]
        |> List.choose id
        |> List.sortBy _.MinutesMin
        |> List.tryHead

    let through (city: Place) (middle: Place) =
        match between last.Slug middle.Slug, between middle.Slug city.Slug with
        | Some first, Some second -> Some(passing middle first second)
        | _ -> None

    let reach (city: Place) =
        between last.Slug city.Slug
        |> Option.orElse (
            catalog.Places
            |> List.choose (through city)
            |> List.sortBy _.MinutesMin
            |> List.tryHead
        )
        |> Option.map (fun link -> city, link)

    let nearest (cities: Place list) =
        cities
        |> List.filter (fun city -> city.Slug <> last.Slug)
        |> List.choose reach
        |> List.sortBy (fun (city, link) -> not (Scoring.isUsableIn request.Month city), link.MinutesMin, city.Slug)
        |> List.tryHead

    if last.Hub then
        None
    else
        match nearest (catalog.Places |> List.filter _.Hub) with
        | None when last.Airport.IsNone -> nearest (catalog.Places |> List.filter _.Airport.IsSome)
        | found -> found
        |> Option.filter (fst >> closes)

/// Takes a city out of the route, so it can come last and not appear twice.
/// The stop after it is now reached by passing through.
let private without (city: Place) (stops: RoutedStop list) =
    match stops |> List.tryFindIndex (fun stop -> stop.Place.Slug = city.Slug) with
    | None -> stops
    | Some index ->
        let onward (stop: RoutedStop) (next: RoutedStop) =
            match stop.Arrival, next.Arrival with
            | Some first, Some second -> Some(passing city first second)
            | _ -> None

        stops
        |> List.indexed
        |> List.choose (fun (at, stop) ->
            if at = index then
                None
            elif at = index + 1 then
                Some {
                    stop with
                        Arrival = onward stops[index] stop
                }
            else
                Some stop)

/// Regions holding a place someone liked are always open. The limit stops sprawl into the others.
let private allowsRegions (catalog: Catalog) (request: TripRequest) liked (ranked: Place list) =
    let likedRegions =
        ranked
        |> List.filter (fun place -> List.contains place.Slug liked)
        |> List.map (regionOf catalog)
        |> Set.ofList

    let spare = max 0 (regionLimit request.Days - likedRegions.Count)
    fun (regions: Set<string>) -> (regions - likedRegions).Count <= spare

/// Chooses connected stops within the allowed regions, with days adding up exactly.
/// `closers` are the cities a trip may end in, which a hard month for sightseeing doesn't rule out.
/// `ranked` are the candidates, best first, and `worthOf` says what one adds after the stops so far.
let route
    (catalog: Catalog)
    (request: TripRequest)
    prefersDepth
    liked
    (closers: Place list)
    (ranked: Place list)
    (worthOf: Place list -> Place -> int)
    : RoutedStop list =
    let slow = prefersDepth || request.Travellers = TravellerType.Family
    let quick = not slow && request.Travellers = TravellerType.Friends

    let stopDays (place: Place) =
        if slow then place.DaysMax
        elif quick then place.DaysMin
        else (place.DaysMin + place.DaysMax + 1) / 2

    let links =
        catalog.Links
        |> List.filter (allowed request)
        |> List.sortBy (fun link -> link.MinutesMin, link.Slug)
        |> List.rev
        |> List.map (fun link -> (link.FromSlug, link.ToSlug), link)
        |> Map.ofList

    let moveCost =
        if slow then slowStopCost
        elif quick then quickStopCost
        else stopCost

    let grow =
        extend
            catalog
            request
            links
            stopDays
            moveCost
            (allowsRegions catalog request liked ranked)
            request.Days
            ranked
            worthOf

    let starts = ranked |> List.map (start catalog stopDays worthOf)

    let stops =
        explore grow starts starts
        |> bestPath request.Days (startCost catalog links)
        |> _.Stops
        |> List.rev

    let fitted total (stops: RoutedStop list) =
        let trimmed = trim (planned stops - total) stops
        fill (total - planned trimmed) 0 trimmed

    let closing = closingDays request.Days
    let beforeClosing = fitted (request.Days - closing) stops

    let endsInHub (route: RoutedStop list) = (List.last route).Place.Hub

    match closingCity catalog request links closers beforeClosing with
    // Making room can drop a stop after a hub, and then the hub is the natural end.
    | None when endsInHub beforeClosing -> fitted request.Days beforeClosing
    | None -> fitted request.Days stops
    | Some(city, link) ->
        fitted (request.Days - closing) (without city beforeClosing)
        @ [
            {
                Place = city
                Days = closing
                Arrival = Some link
            }
        ]
