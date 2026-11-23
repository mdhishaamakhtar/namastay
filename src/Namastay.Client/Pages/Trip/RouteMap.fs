module Namastay.Client.Pages.Trip.RouteMap

open Feliz
open Namastay.Shared
open Namastay.Shared.Contracts

let private box: RouteSketch.Box = {
    Width = 320.0
    Height = 240.0
    Margin = 28.0
}

/// The small whole-country picture that shows where a close-up sits.
let private inset: RouteSketch.Box = {
    Width = 66.0
    Height = 72.0
    Margin = 4.0
}

let private insetGap = 6.0
let private dotRadius = 11.0
let private landmarkRadius = 3.5
let private labelGap = 7.0
/// The room a city's name takes, so two names aren't written over each other.
let private labelWidth = 70.0
let private labelHeight = 14.0
/// A close-up this many times nearer than the whole country gets the inset.
let private closeUp = 1.6

/// One decimal place is finer than a screen can show, and keeps the drawing's text short.
let private rounded (value: float) = System.Math.Round(value, 1)

let private at (x: float, y: float) = $"{rounded x},{rounded y}"

/// The country as one path, every ring closed.
let private country (view: RouteSketch.View) =
    IndiaOutline.rings
    |> List.map (fun ring ->
        let points = ring |> List.map (RouteSketch.toBox view >> at) |> String.concat " L"

        $"M{points} Z")
    |> String.concat " "

let private landmark (found: MapLandmark) (x: float, y: float) =
    // A name near the right edge is written leftwards, so it stays inside the drawing.
    let leftwards = x > box.Width * 0.7

    Svg.g [
        svg.key ("landmark-" + found.Name)
        svg.className "route-landmark"
        svg.children [
            Svg.circle [ svg.cx (rounded x); svg.cy (rounded y); svg.r landmarkRadius ]
            Svg.text [
                svg.x (rounded (if leftwards then x - labelGap else x + labelGap))
                svg.y (rounded (y + 4.0))
                (if leftwards then
                     svg.textAnchor.endOfText
                 else
                     svg.textAnchor.startOfText)
                svg.text found.Name
            ]
        ]
    ]

let private stopDot (number: int) (stop: TripStop) (x: float, y: float) =
    Svg.g [
        svg.key ("stop-" + stop.PlaceSlug)
        svg.children [
            Svg.circle [
                svg.cx (rounded x)
                svg.cy (rounded y)
                svg.r dotRadius
                svg.fill stop.Colours.Deep
                svg.className "route-stop"
            ]
            Svg.text [
                svg.x (rounded x)
                svg.y (rounded (y + 4.0))
                svg.textAnchor.middle
                svg.fill stop.Colours.TextOnDeep
                svg.className "route-number"
                svg.text (string number)
            ]
        ]
    ]

/// The cities whose names have room: clear of the stops, and of the cities already named.
let private roomFor (stops: (float * float) list) (cities: (MapLandmark * (float * float)) list) =
    let crowds (x: float, y: float) (otherX: float, otherY: float) =
        abs (x - otherX) < labelWidth && abs (y - otherY) < labelHeight

    let underStop (x: float, y: float) =
        stops
        |> List.exists (fun (stopX, stopY) -> abs (x - stopX) < dotRadius * 2.0 && abs (y - stopY) < dotRadius * 2.0)

    cities
    |> List.fold
        (fun named (city, point) ->
            if underStop point || named |> List.exists (snd >> crowds point) then
                named
            else
                named @ [ city, point ])
        []

/// The corner with the least under it, so the inset covers as little of the route as it
/// can. Corners that tie are taken in the order given, top right first.
let private insetCorner (busy: (float * float) list) =
    let right = box.Width - inset.Width - insetGap
    let bottom = box.Height - inset.Height - insetGap

    let covered (left: float, top: float) =
        busy
        |> List.filter (fun (x, y) ->
            x > left - dotRadius
            && x < left + inset.Width + dotRadius
            && y > top - dotRadius
            && y < top + inset.Height + dotRadius)
        |> List.length

    [ (right, insetGap); (insetGap, insetGap); (insetGap, bottom); (right, bottom) ]
    |> List.indexed
    |> List.minBy (fun (order, corner) -> covered corner, order)
    |> snd

/// The whole country, small, with the close-up's ground marked on it.
let private whereInIndia (view: RouteSketch.View) (busy: (float * float) list) =
    match RouteSketch.fit box IndiaOutline.extent, RouteSketch.fit inset IndiaOutline.extent with
    | Some wholeHere, Some whole when view.Scale > wholeHere.Scale * closeUp ->
        let left, top = insetCorner busy
        let southWest, northEast = RouteSketch.corners box view
        let west, south = RouteSketch.toBox whole southWest
        let east, north = RouteSketch.toBox whole northEast
        let within low high value = max low (min high value)
        let markLeft, markRight = within 0.0 inset.Width west, within 0.0 inset.Width east

        let markTop, markBottom =
            within 0.0 inset.Height north, within 0.0 inset.Height south

        Svg.g [
            svg.className "route-inset"
            svg.custom ("transform", $"translate({rounded left} {rounded top})")
            svg.children [
                Svg.rect [
                    svg.width inset.Width
                    svg.height inset.Height
                    svg.className "route-inset-back"
                ]
                Svg.path [ svg.d (country whole); svg.className "route-land" ]
                Svg.rect [
                    svg.x (rounded markLeft)
                    svg.y (rounded markTop)
                    svg.width (rounded (markRight - markLeft))
                    svg.height (rounded (markBottom - markTop))
                    svg.className "route-inset-mark"
                ]
            ]
        ]
    | _ -> Html.none

/// The route where it really runs: India behind it, each stop by its number, joined in
/// order, and the main cities nearby marked faintly. A stop with no known position is
/// left out, and the line runs past it.
let private sketch (trip: Trip) =
    let stops =
        trip.Stops
        |> List.indexed
        |> List.choose (fun (index, stop) -> stop.Position |> Option.map (fun position -> index + 1, stop, position))

    match RouteSketch.fit box (stops |> List.map (fun (_, _, position) -> position)) with
    | None -> Html.none
    | Some view ->
        // Stops close together on the ground would cover each other, so they're eased apart.
        let stopPoints =
            stops
            |> List.map (fun (_, _, position) -> RouteSketch.toBox view position)
            |> RouteSketch.easeApart (dotRadius * 2.0)

        let landmarks =
            trip.Landmarks
            |> List.filter (fun found -> RouteSketch.shows box view found.Position)
            |> List.map (fun found -> found, RouteSketch.toBox view found.Position)
            |> roomFor stopPoints

        Svg.svg [
            svg.viewBox (0, 0, int box.Width, int box.Height)
            svg.className "route-sketch"
            svg.custom ("aria-hidden", "true")
            svg.children [
                Svg.path [ svg.d (country view); svg.className "route-land" ]
                yield! landmarks |> List.map (fun (found, point) -> landmark found point)
                Svg.polyline [
                    svg.points (stopPoints |> List.map at |> String.concat " ")
                    svg.className "route-line"
                ]
                yield! List.map2 (fun (number, stop, _) point -> stopDot number stop point) stops stopPoints
                whereInIndia view (stopPoints @ List.map snd landmarks)
            ]
        ]

let private row (number: int) (stop: TripStop) =
    Html.li [
        prop.key stop.PlaceSlug
        prop.children [
            Html.span [
                prop.className "route-dot"
                prop.style [ style.backgroundColor stop.Colours.Deep; style.color stop.Colours.TextOnDeep ]
                prop.custom ("aria-hidden", "true")
                prop.text number
            ]
            Html.span stop.Name
        ]
    ]

/// The route drawn where it runs, then the stops in order, numbered to match.
let view (trip: Trip) =
    Html.div [
        prop.className "route-card"
        prop.children [
            sketch trip
            Html.ol [
                prop.className "route"
                prop.custom ("aria-label", "Your route")
                prop.children (trip.Stops |> List.mapi (fun index stop -> row (index + 1) stop))
            ]
        ]
    ]
