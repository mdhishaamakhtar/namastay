/// The marks printed on Indian menus, so the page teaches what to look for.
module Namastay.Client.Components.DietMark

open Feliz
open Namastay.Shared
open Namastay.Shared.Contracts

let private green = "#1F7A3D"
let private brown = "#7A3E12"

let private frame (fill: string) (colour: string) =
    Svg.rect [
        svg.x 1
        svg.y 1
        svg.width 14
        svg.height 14
        svg.fill fill
        svg.stroke colour
        svg.strokeWidth 2
    ]

let private inside kind = [
    match kind with
    | DishKind.Vegan ->
        frame green green

        Svg.path [
            svg.d "M4 11C4 6 8 4 12 4c0 4-2 8-6.5 7.5M5 11l4-4"
            svg.fill "none"
            svg.stroke "#FFFFFF"
            svg.strokeWidth 1.5
        ]
    | DishKind.Vegetarian ->
        frame "#FFFFFF" green
        Svg.circle [ svg.cx 8; svg.cy 8; svg.r 3.6; svg.fill green ]
    | DishKind.NonVeg ->
        frame "#FFFFFF" brown
        Svg.path [ svg.d "M8 4l4.5 8h-9z"; svg.fill brown ]
]

let mark kind =
    Svg.svg [
        svg.width 14
        svg.height 14
        svg.viewBox (0, 0, 16, 16)
        svg.className "diet-mark"
        svg.custom ("aria-hidden", "true")
        svg.children (inside kind)
    ]

/// The word is always there, so the mark never carries the meaning alone.
let label kind =
    Html.span [
        prop.className "diet-label"
        prop.children [ mark kind; Html.span (DishKinds.name kind) ]
    ]
