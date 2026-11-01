/// Waiting states. Each one is its page with shapes in place of content, so nothing jumps when data arrives.
module Namastay.Client.Components.Skeletons

open Feliz

let private bone (size: string) =
    Html.span [ prop.className $"bone {size}" ]

/// A line of text that isn't known yet, where a page has one to wait for.
let line = bone "bone-line"

/// A real button shape, so the page around it doesn't move when the real one arrives.
let private button =
    Html.span [ prop.className "button trip-share skeleton-button"; prop.text "\u00A0" ]

let private waiting (label: string) (className: string) (children: ReactElement list) =
    Html.div [
        prop.className className
        prop.role "status"
        prop.ariaLabel label
        prop.children children
    ]

let private stop index =
    Html.li [
        prop.key (string index)
        prop.className "stop"
        prop.children [
            Html.div [
                prop.className "stop-card"
                prop.children [
                    Html.div [
                        prop.className "stop-bones"
                        prop.children [
                            Html.span [
                                prop.className "stop-title"
                                prop.children [ bone "bone-short"; bone "bone-line" ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

/// The trip page, for opening a trip and for the moment one is being built.
let trip (label: string) =
    waiting label "trip" [
        Html.aside [
            prop.className "trip-panel"
            prop.children [
                Html.span [ prop.className "journey-brand"; prop.text "Namastay" ]
                bone "bone-heading"
                Html.div [ prop.className "route"; prop.children [ for _ in 1..4 -> bone "bone-route" ] ]
                Html.div [
                    prop.className "trip-mix"
                    prop.children [ bone "bone-subheading"; bone "bone-track"; bone "bone-track" ]
                ]
                button
                button
                button
            ]
        ]
        Html.div [
            prop.className "trip-content"
            prop.children [
                bone "bone-best"
                Html.ol [
                    prop.className "trip-stops"
                    prop.children (stop 1 :: bone "bone-open" :: List.map stop [ 2; 3; 4 ])
                ]
            ]
        ]
    ]

let private savedTrip index =
    Html.li [
        prop.key (string index)
        prop.className "my-trip"
        prop.children [
            Html.div [ prop.className "my-trip-photo bone" ]
            bone "bone-title"
            bone "bone-line"
        ]
    ]

/// The cards on My trips. The panel beside them doesn't wait for anything.
let myTrips (label: string) =
    waiting label "my-trips" [ for index in 1..2 -> savedTrip index ]

/// The page for changing a trip.
let editor (label: string) =
    waiting label "trip" [
        Html.aside [
            prop.className "trip-panel"
            prop.children [ bone "bone-short"; bone "bone-heading"; bone "bone-track"; bone "bone-input" ]
        ]
        Html.div [
            prop.className "trip-content"
            prop.children [ bone "bone-stop"; bone "bone-stop"; bone "bone-stop" ]
        ]
    ]

/// The swipe card and its buttons.
let swipeCard (label: string) =
    waiting label "journey-stage" [
        Html.div [
            prop.className "journey-mix"
            prop.children [ bone "bone-title"; bone "bone-line"; bone "bone-line" ]
        ]
        Html.div [
            prop.className "card-label"
            prop.children [
                Html.div [
                    prop.className "card-frame"
                    prop.children [
                        Html.div [ prop.className "card-photo bone" ]
                        Html.div [
                            prop.className "card-text"
                            prop.children [ bone "bone-title"; bone "bone-line" ]
                        ]
                    ]
                ]
            ]
        ]
        Html.div [
            prop.className "journey-actions"
            prop.children [ bone "bone-input"; bone "bone-input" ]
        ]
    ]

/// The feeling tiles on the home page.
let feelings (label: string) =
    waiting label "moods" [
        for index in 1..14 -> Html.div [ prop.key (string index); prop.className "mood bone" ]
    ]
