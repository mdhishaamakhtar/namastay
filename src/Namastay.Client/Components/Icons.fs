module Namastay.Client.Components.Icons

open Feliz
open Namastay.Shared.Contracts

let chevron (flipped: bool) =
    Svg.svg [
        svg.width 22
        svg.height 22
        svg.viewBox (0, 0, 20, 20)
        svg.className (if flipped then "chevron chevron-open" else "chevron")
        svg.custom ("aria-hidden", "true")
        svg.children [
            Svg.path [
                svg.d "M4 7l6 6l6 -6"
                svg.fill "none"
                svg.stroke "currentColor"
                svg.strokeWidth 2
            ]
        ]
    ]

let private lineIcon (paths: string list) =
    Svg.svg [
        svg.width 18
        svg.height 18
        svg.viewBox (0, 0, 24, 24)
        svg.custom ("aria-hidden", "true")
        svg.children (
            paths
            |> List.map (fun path ->
                Svg.path [
                    svg.d path
                    svg.fill "none"
                    svg.stroke "currentColor"
                    svg.strokeWidth 2.2
                    svg.strokeLineCap "round"
                    svg.strokeLineJoin "round"
                ])
        )
    ]

let undo = lineIcon [ "M9 7L4 12l5 5"; "M4 12h11a5 5 0 0 1 5 5v1" ]
let back = lineIcon [ "M15 6l-6 6 6 6" ]

/// The Google mark, in Google's own colours, as their brand rules ask.
let google =
    let part (colour: string) (path: string) =
        Svg.path [ svg.fill colour; svg.d path ]

    Svg.svg [
        svg.width 20
        svg.height 20
        svg.viewBox (0, 0, 48, 48)
        svg.custom ("aria-hidden", "true")
        svg.children [
            part
                "#EA4335"
                "M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"
            part
                "#4285F4"
                "M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"
            part
                "#FBBC05"
                "M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"
            part
                "#34A853"
                "M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"
        ]
    ]

let private travelPaths =
    function
    | TravelMode.Train -> [
        "M8 3h8a3 3 0 0 1 3 3v8a3 3 0 0 1-3 3H8a3 3 0 0 1-3-3V6a3 3 0 0 1 3-3Z"
        "M5 11h14M12 3v8"
        "M8.5 14h.01M15.5 14h.01"
        "M8 17l-2 4M16 17l2 4M7 19.5h10"
      ]
    | TravelMode.Flight -> [
        "M21 3c-1.5-.6-3 .1-4 1.1L13.5 7.6 5 5.5 3.5 7l7 3.8-3.2 3.2-2.8-.6L3.3 14.6l3.8 2.1 2.1 3.8 1.2-1.2-.6-2.8 3.2-3.2 3.8 7 1.5-1.5-2.1-8.5 3.5-3.5c1-1 1.7-2.5 1.1-4.1Z"
      ]
    | TravelMode.Road -> [
        "M5 16V11l2-5h10l2 5v5"
        "M4 11h16M4 16h16"
        "M7 16v3M17 16v3"
        "M7.5 13.5h.01M16.5 13.5h.01"
      ]
    | TravelMode.Ferry -> [
        "M12 3v5M7 8h10l2 5H5l2-5Z"
        "M4 13l2 5h12l2-5"
        "M3 21c2 0 2-1 4.5-1s2.5 1 4.5 1 2-1 4.5-1 2.5 1 4.5 1"
      ]
    | TravelMode.CableCar -> [
        "M3 6l18-3"
        "M12 4.5V9"
        "M7 9h10a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2Z"
        "M5 14h14M12 9v5"
      ]

/// A way of getting between stops, drawn on the line that joins them.
let travel (mode: TravelMode) =
    Svg.svg [
        svg.width 26
        svg.height 26
        svg.viewBox (0, 0, 24, 24)
        svg.custom ("aria-hidden", "true")
        svg.fill "none"
        svg.stroke "currentColor"
        svg.strokeWidth 1.8
        svg.strokeLineCap "round"
        svg.strokeLineJoin "round"
        svg.children [ for path in travelPaths mode -> Svg.path [ svg.key path; svg.d path ] ]
    ]
