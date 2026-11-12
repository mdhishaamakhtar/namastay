/// The scorecard: what the planner does across many travellers, each line with the way it should move.
module Namastay.Bench.Metrics

open Namastay.Bench.Personas
open Namastay.Bench.Session
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type Better =
    | Higher
    | Lower
    | Zero

type Line = {
    Group: string
    Name: string
    Value: float
    Better: Better
}

let private line group name value better = {
    Group = group
    Name = name
    Value = value
    Better = better
}

let private percent (part: int) (whole: int) =
    if whole = 0 then 0.0 else float part * 100.0 / float whole

let private mean (values: float list) =
    if values.IsEmpty then 0.0 else List.average values

let private built (sessions: Session list) =
    sessions
    |> List.choose (fun session ->
        match session.Trip with
        | Ok trip -> Some(session, trip)
        | Error _ -> None)

let private stopSlugs (trip: Trip) = trip.Stops |> List.map _.PlaceSlug

let private placeOf (catalog: Catalog) (slug: string) =
    catalog.Places |> List.tryFind (fun place -> place.Slug = slug)

let private variety (catalog: Catalog) (sessions: Session list) =
    let published = catalog.Places |> List.filter _.Published |> List.length
    let routes = built sessions |> List.map (snd >> stopSlugs)

    let firstCards =
        sessions
        |> List.filter (fun session -> session.Persona.Feelings.IsEmpty)
        |> List.choose (fun session -> session.Shown |> List.tryHead)
        |> List.map _.Slug
        |> List.distinct
        |> List.length

    let reached = routes |> List.concat |> List.distinct |> List.length
    let openings = routes |> List.choose List.tryHead |> List.distinct |> List.length

    let topShare =
        routes
        |> List.collect List.distinct
        |> List.countBy id
        |> List.map snd
        |> List.fold max 0

    [
        line "variety" "first cards, open deck" (float firstCards) Better.Higher
        line "variety" "distinct trips %" (percent (List.distinct routes).Length routes.Length) Better.Higher
        line "variety" "places reached %" (percent reached published) Better.Higher
        line "variety" "places that open a trip" (float openings) Better.Higher
        line "variety" "top place in trips %" (percent topShare routes.Length) Better.Lower
    ]

/// A place that suits an interest strongly, or a child of it.
let private strongIn (catalog: Catalog) (wanted: string list) (place: Place) =
    place.Interests
    |> List.exists (fun item ->
        item.Weight >= Scope.strongFit
        && Swipers.suits catalog.Interests wanted item.InterestSlug)

let private hits catalog wanted (trip: Trip) =
    trip.Stops
    |> List.choose (fun stop -> placeOf catalog stop.PlaceSlug)
    |> List.exists (strongIn catalog wanted)

let private daysOnTaste catalog (persona: Persona) (trip: Trip) =
    let onTaste =
        trip.Stops
        |> List.filter (fun stop ->
            placeOf catalog stop.PlaceSlug
            |> Option.exists (strongIn catalog persona.Interests))
        |> List.sumBy _.Days

    percent onTaste (trip.Stops |> List.sumBy _.Days)

/// A stop whose every shown card was passed.
let private passedStop (session: Session) (trip: Trip) =
    trip.Stops
    |> List.exists (fun stop ->
        let here =
            session.Answers
            |> List.filter (fun answer ->
                session.Shown
                |> List.exists (fun card -> card.Slug = answer.Card && card.Place = Some stop.PlaceSlug))

        not here.IsEmpty && here |> List.forall (fun answer -> not answer.Liked))

let private direction (catalog: Catalog) (sessions: Session list) =
    let trips = built sessions

    let tasted =
        trips |> List.filter (fun (session, _) -> session.Persona.Swiper = Swiper.Taste)

    let topHit =
        tasted
        |> List.filter (fun (session, trip) -> hits catalog (List.truncate 1 session.Persona.Interests) trip)
        |> List.length

    let allHit =
        tasted
        |> List.filter (fun (session, trip) ->
            session.Persona.Interests
            |> List.forall (fun wanted -> hits catalog [ wanted ] trip))
        |> List.length

    let scopeCards =
        sessions
        |> List.filter (fun session -> not session.Persona.Feelings.IsEmpty)
        |> List.collect (fun session ->
            session.Shown
            |> List.map (fun shown ->
                catalog.Cards
                |> List.exists (fun card ->
                    card.Slug = shown.Slug
                    && Swipers.matches catalog (List.truncate 1 session.Persona.Interests) card)))

    let passed =
        trips
        |> List.filter (fun (session, trip) -> passedStop session trip)
        |> List.length

    [
        line "direction" "top interest in trip %" (percent topHit tasted.Length) Better.Higher
        line "direction" "every interest in trip %" (percent allHit tasted.Length) Better.Higher
        line
            "direction"
            "days on taste %"
            (tasted
             |> List.map (fun (session, trip) -> daysOnTaste catalog session.Persona trip)
             |> mean)
            Better.Higher
        line
            "direction"
            "scope cards shown %"
            (percent (scopeCards |> List.filter id |> List.length) scopeCards.Length)
            Better.Higher
        line "direction" "passed place in trip %" (percent passed trips.Length) Better.Lower
    ]

let private legs (trip: Trip) =
    (trip.Stops |> List.choose _.Arrival)
    @ (trip.Arrive |> Option.bind _.Leg |> Option.toList)
    @ (trip.Depart |> Option.bind _.Leg |> Option.toList)

let private hours (leg: TravelLeg) = float leg.MinutesMin / 60.0

let private avoidStops (catalog: Catalog) (session: Session) (trip: Trip) =
    match session.Persona.Month with
    | None -> 0
    | Some month ->
        trip.Stops
        |> List.filter (fun stop ->
            placeOf catalog stop.PlaceSlug
            |> Option.exists (fun place -> Scoring.monthRating month place = Rating.Avoid))
        |> List.length

let private freeDays (trip: Trip) =
    trip.Stops
    |> List.collect _.Plans
    |> List.filter (fun day -> day.Plan = Builder.spareDay)
    |> List.length

/// A city visited twice, or an airport city that's also a stop in the middle of the route.
/// Landing somewhere and flying home from it is a normal out-and-back, so the ends don't count.
let private cityTwice (trip: Trip) =
    let slugs = stopSlugs trip

    let middle =
        match trip.Stops |> List.map _.Name with
        | _ :: rest when rest.Length >= 2 -> rest |> List.take (rest.Length - 1)
        | _ -> []

    let inside (gateway: TripGateway option) =
        gateway |> Option.exists (fun found -> List.contains found.City middle)

    (List.distinct slugs).Length < slugs.Length
    || inside trip.Arrive
    || inside trip.Depart

let private comfort (catalog: Catalog) (sessions: Session list) =
    let trips = built sessions

    let count predicate =
        trips |> List.filter (snd >> predicate) |> List.length

    [
        line
            "comfort"
            "travel hours per trip"
            (trips |> List.map (fun (_, trip) -> legs trip |> List.sumBy hours) |> mean)
            Better.Lower
        line
            "comfort"
            "longest leg hours"
            (trips
             |> List.map (fun (_, trip) -> legs trip |> List.map hours |> List.fold max 0.0)
             |> mean)
            Better.Lower
        line
            "comfort"
            "avoid-month stops per trip"
            (trips
             |> List.map (fun (session, trip) -> float (avoidStops catalog session trip))
             |> mean)
            Better.Lower
        line "comfort" "free days per trip" (trips |> List.map (snd >> freeDays >> float) |> mean) Better.Lower
        line "comfort" "city twice %" (percent (count cityTwice) trips.Length) Better.Zero
        line
            "comfort"
            "swaps per trip"
            (trips |> List.map (fun (_, trip) -> float trip.Swaps.Length) |> mean)
            Better.Lower
        line "comfort" "trips not built" (float (sessions.Length - trips.Length)) Better.Zero
    ]

let private ofSwiper (swiper: Swiper) (sessions: Session list) =
    sessions |> List.filter (fun session -> session.Persona.Swiper = swiper)

let private settledShare (sessions: Session list) =
    percent (sessions |> List.filter _.Early.IsSome |> List.length) sessions.Length

/// Whether the offer to build early reaches the travellers with a clear taste, how soon,
/// and whether the trip they'd get then still follows that taste.
let private pace (catalog: Catalog) (sessions: Session list) =
    let tasted = ofSwiper Swiper.Taste sessions

    let early =
        tasted
        |> List.choose (fun session -> session.Early |> Option.map (fun finish -> session, finish))

    let earlyHit =
        early
        |> List.filter (fun (session, finish) ->
            match finish.Trip with
            | Ok trip -> hits catalog (List.truncate 1 session.Persona.Interests) trip
            | Error _ -> false)
        |> List.length

    [
        line "pace" "taste settles early %" (settledShare tasted) Better.Higher
        line "pace" "cards to settle" (early |> List.map (fun (_, finish) -> float finish.Cards) |> mean) Better.Lower
        line "pace" "top interest in early trip %" (percent earlyHit early.Length) Better.Higher
        line "pace" "coin flip settles early %" (settledShare (ofSwiper Swiper.Random sessions)) Better.Lower
    ]

let private openingCards = 6

/// The share of the opening cards that speak for the feeling seen least, out of those
/// that speak for either. Half is an even spread.
let private lesserShare (catalog: Catalog) (session: Session) =
    let speaks (feeling: string) (shown: JourneyCard) =
        catalog.Cards
        |> List.exists (fun card -> card.Slug = shown.Slug && Swipers.matches catalog [ feeling ] card)

    let opening = session.Shown |> List.truncate openingCards

    let counts =
        session.Persona.Feelings
        |> List.map (fun feeling -> opening |> List.filter (speaks feeling) |> List.length)

    percent (List.min counts) (List.sum counts)

/// Travellers who planned from several feelings: whether the deck opens on all of them
/// and the trip covers each, not whichever the swipes happened to favour.
let private several (catalog: Catalog) (sessions: Session list) =
    let trips = built sessions

    let every =
        trips
        |> List.filter (fun (session, trip) ->
            session.Persona.Feelings
            |> List.forall (fun feeling -> hits catalog [ feeling ] trip))
        |> List.length

    [
        line "several" "every feeling in trip %" (percent every trips.Length) Better.Higher
        line "several" "opening, lesser feeling %" (sessions |> List.map (lesserShare catalog) |> mean) Better.Higher
        line
            "several"
            "travel hours per mixed trip"
            (trips |> List.map (fun (_, trip) -> legs trip |> List.sumBy hours) |> mean)
            Better.Lower
        line "several" "mixed trips not built" (float (sessions.Length - trips.Length)) Better.Zero
    ]

/// The lines every run is judged by come from travellers planning from one feeling or none,
/// so they compare with earlier baselines. Those planning from several have their own group.
let scorecard (catalog: Catalog) (sessions: Session list) : Line list =
    let mixed, main =
        sessions |> List.partition (fun session -> session.Persona.Feelings.Length > 1)

    variety catalog main
    @ direction catalog main
    @ comfort catalog main
    @ pace catalog main
    @ several catalog mixed
