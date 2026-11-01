module Namastay.Client.Components.Icons

open Feliz

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
