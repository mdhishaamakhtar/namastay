module Namastay.Client.Theme.Theme

open Browser
open Fable.Core.JsInterop
open Namastay.Shared.Contracts

let private setColour (name: string) (value: string) =
    document.documentElement?style?setProperty(name, value)

/// Colours come from the API. Nothing about places is coded in the client.
let apply (colours: PlaceColours) =
    setColour "--deep" colours.Deep
    setColour "--mid" colours.Mid
    setColour "--pale" colours.Pale
    setColour "--on-deep" colours.TextOnDeep

/// For a page whose colours aren't known yet, so it never flashes the wrong one.
let neutral () =
    apply {
        Deep = "#EFE6D0"
        Mid = "#EFE6D0"
        Pale = "#FFF8E6"
        TextOnDeep = "#2B2108"
    }

let private storageKey (path: string) = "colours:" + path

/// Storage can be blocked or full. Colours are only a nicety, so that's never an error.
let remember (path: string) (colours: PlaceColours) =
    try
        let text =
            String.concat "|" [ colours.Deep; colours.Mid; colours.Pale; colours.TextOnDeep ]

        localStorage.setItem (storageKey path, text)
    with _ ->
        ()

let recall (path: string) =
    try
        match (localStorage.getItem (storageKey path) |> string).Split '|' with
        | [| deep; mid; pale; text |] ->
            Some {
                Deep = deep
                Mid = mid
                Pale = pale
                TextOnDeep = text
            }
        | _ -> None
    with _ ->
        None

/// The colours a trip is shown in.
let ofTrip (trip: Trip) =
    trip.Stops
    |> List.tryFind (fun stop -> stop.FamilySlug = trip.BaseFamily)
    |> Option.map _.Colours

/// The journey stage. None returns it to brand marigold.
let stage (colours: PlaceColours option) =
    match colours with
    | Some chosen ->
        setColour "--stage" chosen.Deep
        setColour "--on-stage" chosen.TextOnDeep
    | None ->
        document.documentElement?style?removeProperty "--stage"
        document.documentElement?style?removeProperty "--on-stage"

/// Back to the brand colours from styles.css.
let reset () =
    for name in [ "--deep"; "--mid"; "--pale"; "--on-deep" ] do
        document.documentElement?style?removeProperty name
