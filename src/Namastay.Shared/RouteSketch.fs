/// Where a trip's stops, and the country around them, fall in a drawing of its route.
module Namastay.Shared.RouteSketch

open System
open Namastay.Shared.Contracts

type Box = {
    Width: float
    Height: float
    Margin: float
}

/// How the ground maps onto a box: the scale, and the ground under the box's top-left corner.
type View = {
    Scale: float
    West: float
    North: float
}

/// India's middle latitude. A degree of longitude is shorter away from the equator,
/// so east-west distances are narrowed by this to keep the country's shape.
let private middleLatitude = 22.0

let private narrowing = Math.Cos(middleLatitude * Math.PI / 180.0)

/// The least ground a drawing covers, in degrees of latitude, so stops close together
/// still have the coast or the border around them to place them by.
let private leastSpan = 5.0

/// Flat ground east and north, in degrees of latitude.
let private ground (point: MapPoint) =
    point.Longitude * narrowing, point.Latitude

/// A view that holds every point inside the margin, north up, centred on them.
let fit (box: Box) (points: MapPoint list) : View option =
    match points |> List.map ground with
    | [] -> None
    | found ->
        let easts = found |> List.map fst
        let norths = found |> List.map snd
        let west, east = List.min easts, List.max easts
        let south, north = List.min norths, List.max norths

        let scale =
            min
                ((box.Width - 2.0 * box.Margin) / max leastSpan (east - west))
                ((box.Height - 2.0 * box.Margin) / max leastSpan (north - south))

        Some {
            Scale = scale
            West = (west + east) / 2.0 - box.Width / 2.0 / scale
            North = (south + north) / 2.0 + box.Height / 2.0 / scale
        }

/// Where a point falls in the box. A point outside the view falls outside the box.
let toBox (view: View) (point: MapPoint) : float * float =
    let east, north = ground point
    (east - view.West) * view.Scale, (view.North - north) * view.Scale

/// Whether a point is in the picture, clear of the edge by half the margin.
let shows (box: Box) (view: View) (point: MapPoint) : bool =
    let x, y = toBox view point
    let edge = box.Margin / 2.0
    x >= edge && x <= box.Width - edge && y >= edge && y <= box.Height - edge

/// The south-west and north-east corners of the ground a view covers.
let corners (box: Box) (view: View) : MapPoint * MapPoint =
    let south = view.North - box.Height / view.Scale
    let east = view.West + box.Width / view.Scale

    {
        Latitude = south
        Longitude = view.West / narrowing
    },
    {
        Latitude = view.North
        Longitude = east / narrowing
    }

let private easingRounds = 40
/// Two dots this close are in the same place, and need a direction to part in.
let private sameSpot = 0.01
/// Each dot in a pile leaves by its own direction, a golden angle on from the last.
let private goldenAngle = 2.399963

/// How far one dot is pushed by another closer than the gap: half the overlap, straight away from it.
let private push (gap: float) (index: int, (x: float, y: float)) (other: int, (otherX: float, otherY: float)) =
    let distance = sqrt ((x - otherX) ** 2.0 + (y - otherY) ** 2.0)

    if index = other || distance >= gap then
        0.0, 0.0
    elif distance < sameSpot then
        let angle = float index * goldenAngle
        Math.Cos angle * gap / 2.0, Math.Sin angle * gap / 2.0
    else
        let overlap = (gap - distance) / 2.0
        (x - otherX) / distance * overlap, (y - otherY) / distance * overlap

/// Moves dots that would cover each other until they sit `gap` apart, each as near to
/// where it belongs as the others allow. Dots with room between them don't move.
let easeApart (gap: float) (points: (float * float) list) : (float * float) list =
    let round (current: (float * float) list) =
        let numbered = List.indexed current

        numbered
        |> List.map (fun (index, (x, y)) ->
            numbered
            |> List.map (push gap (index, (x, y)))
            |> List.fold (fun (movedX, movedY) (byX, byY) -> movedX + byX, movedY + byY) (x, y))

    List.fold (fun current _ -> round current) points [ 1..easingRounds ]
