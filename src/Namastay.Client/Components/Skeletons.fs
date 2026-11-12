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

/// Real elements with hidden text, so a skeleton takes the size the page will.
let private blank = "\u00A0"

let private hiddenText (text: string) = [ prop.className "bone-text"; prop.text text ]

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
                                // Days, the name, and the line that says why the stop is there.
                                prop.children [ bone "bone-short"; bone "bone-line"; bone "bone-why" ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

/// A line between stops: the round mark, then two lines of text.
let private leg (key: string) =
    Html.li [
        prop.key key
        prop.className "stop stop-airport"
        prop.children [
            Html.div [
                prop.className "stop-leg"
                prop.children [
                    Html.div [
                        prop.className "stop-leg-line"
                        prop.children [ Html.span [ prop.className "stop-leg-mark" ] ]
                    ]
                    Html.div [
                        prop.className "stop-leg-plain"
                        prop.children [
                            Html.span [
                                prop.className "stop-leg-text"
                                prop.children [
                                    Html.strong (hiddenText "Train to somewhere")
                                    Html.span (hiddenText "About 3 hours")
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private routeRow index =
    Html.li [
        prop.key (string index)
        prop.children [ Html.span [ prop.className "route-dot" ]; Html.span (hiddenText "A place") ]
    ]

let private mixRow index =
    Html.div [
        prop.key (string index)
        prop.className "mix-row"
        prop.children [ Html.span (hiddenText "A feeling"); Html.div [ prop.className "mix-track" ] ]
    ]

/// The trip page, for opening a trip and for the moment one is being built.
let trip (label: string) (buttons: int) =
    waiting label "trip" [
        Html.aside [
            prop.className "trip-panel"
            prop.children [
                Html.h1 (hiddenText "14 days in Rajasthan and North India")
                Html.ol [
                    prop.className "route"
                    prop.children [ for index in 1..6 -> routeRow index ]
                ]
                Html.div [
                    prop.className "trip-mix"
                    prop.children [ Html.h2 (hiddenText "Your mix"); yield! List.map mixRow [ 1; 2; 3 ] ]
                ]
                Html.div [ prop.className "trip-actions"; prop.children (List.replicate buttons button) ]
            ]
        ]
        Html.div [
            prop.className "trip-content"
            prop.children [
                bone "bone-best"
                Html.ol [
                    prop.className "trip-stops"
                    prop.children [
                        leg "land"
                        leg "land-leg"
                        stop 1
                        bone "bone-open"
                        for index in 2..5 do
                            leg $"leg-{index}"
                            stop index
                        leg "home"
                    ]
                ]
            ]
        ]
    ]

let private savedTrip index =
    Html.li [
        prop.key (string index)
        prop.className "my-trip"
        prop.children [ Html.div [ prop.className "label bone" ]; bone "bone-line" ]
    ]

/// The labels on My trips. The header above them doesn't wait for anything.
let myTrips (label: string) =
    waiting label "my-trips" [ for index in 1..3 -> savedTrip index ]

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

let private browseHead (section: Menu.Section) (title: string) (extra: ReactElement list) =
    Html.div [
        prop.className "browse-head"
        prop.children [
            Menu.view section
            Html.div [
                prop.className "browse-hero"
                prop.children [
                    Html.div [
                        prop.className "browse-title"
                        prop.children [
                            Html.h1 (hiddenText title)
                            Html.p (hiddenText "A short line about this page.")
                            Html.div [
                                prop.className "browse-plan"
                                prop.children [
                                    Html.span [ prop.className "button skeleton-button"; prop.text "Plan a trip" ]
                                ]
                            ]
                        ]
                    ]
                    yield! extra
                ]
            ]
        ]
    ]

let private regionCard index =
    Html.div [
        prop.key (string index)
        prop.className "region-card"
        prop.children [
            Html.div [
                prop.className "region-top"
                prop.children [
                    Html.div [ prop.className "bone bone-region" ]
                    Html.span [
                        prop.className "region-name"
                        prop.children [ Html.strong (hiddenText "The Himalaya") ]
                    ]
                ]
            ]
            Html.ul [
                prop.children [
                    for row in 1..5 ->
                        Html.li [
                            prop.key (string row)
                            prop.children [
                                Html.span [
                                    prop.className "region-line"
                                    prop.children [ Html.span (hiddenText "A place name") ]
                                ]
                            ]
                        ]
                ]
            ]
            Html.span [
                prop.className "region-all"
                prop.children [ Html.span (hiddenText "All 10 places") ]
            ]
        ]
    ]

let private regions =
    Html.div [
        prop.className "region-grid"
        prop.children [ for index in 1..8 -> regionCard index ]
    ]

let private search = Html.div [ prop.className "search-input bone" ]

let private listing index =
    Html.div [
        prop.key (string index)
        prop.className "listing"
        prop.children [
            Html.span [ prop.className "listing-colour bone" ]
            Html.span [
                prop.className "listing-text"
                prop.children [
                    Html.strong (hiddenText "A place")
                    Html.span (hiddenText "A sentence about why this place is here, long enough to fill two lines.")
                ]
            ]
        ]
    ]

/// Places: the search box and all eight regions.
let places (label: string) =
    waiting label "browse" [
        browseHead Menu.Section.Places "68 places across India" []
        Html.div [ prop.className "browse-body"; prop.children [ search; regions ] ]
    ]

/// Food: the search box, the leading dishes, then the regions.
let food (label: string) =
    waiting label "browse" [
        browseHead Menu.Section.Food "Eat your way across India" []
        Html.div [
            prop.className "browse-body"
            prop.children [
                search
                Html.div [
                    prop.className "label-grid home-food-grid"
                    prop.children [
                        for index in 1..3 -> Html.div [ prop.key (string index); prop.className "label bone" ]
                    ]
                ]
                Html.h2 (hiddenText "By region")
                regions
            ]
        ]
    ]

/// A region, feeling or dish page: its drawing, then its list.
let listed (label: string) (section: Menu.Section) =
    waiting label "browse" [
        browseHead section "A page title" [
            Html.div [
                prop.className "browse-art"
                prop.children [ Html.div [ prop.className "bone bone-region" ] ]
            ]
        ]
        Html.div [
            prop.className "browse-body"
            prop.children [
                Html.h2 (hiddenText "Places here")
                Html.div [
                    prop.className "listings"
                    prop.children [ for index in 1..8 -> listing index ]
                ]
            ]
        ]
    ]

/// Home for someone signed in: their latest trip, the ways onward, then the feelings.
let signedHome (label: string) =
    waiting label "browse" [
        Html.div [
            prop.className "browse-head"
            prop.children [
                Menu.view Menu.Section.Home
                Html.div [
                    prop.className "browse-title"
                    prop.children [ Html.h1 "Your India"; Html.p "Welcome back." ]
                ]
            ]
        ]
        Html.div [
            prop.className "signed-main"
            prop.children [
                Html.div [
                    prop.className "signed-heading"
                    prop.children [ Html.h2 "Your latest trip" ]
                ]
                Html.div [ prop.className "label bone signed-trip" ]
                Html.div [
                    prop.className "signed-next"
                    prop.children [
                        Html.span [ prop.className "button home-plan skeleton-button"; prop.text blank ]
                        search
                        Html.div [
                            prop.className "doors"
                            prop.children [
                                Html.div [ prop.className "door bone" ]
                                Html.div [ prop.className "door bone" ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

/// The account page's rows.
let account (label: string) =
    waiting label "account-details" [
        bone "bone-subheading"
        bone "bone-line"
        bone "bone-subheading"
        bone "bone-input"
        bone "bone-input"
    ]

/// The feeling tiles on the home page.
let feelings (label: string) =
    waiting label "moods" [
        for index in 1..14 -> Html.div [ prop.key (string index); prop.className "mood bone" ]
    ]
