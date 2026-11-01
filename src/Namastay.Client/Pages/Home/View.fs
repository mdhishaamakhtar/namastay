module Namastay.Client.Pages.Home.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Home.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private colourBar (colour: string) =
    Html.span [ prop.className "result-colour"; prop.style [ style.backgroundColor colour ] ]

let private resultText (place: PlaceSummary) (note: string) =
    Html.span [
        prop.className "result-text"
        prop.children [ Html.strong place.Name; Html.span note ]
    ]

let private result (place: PlaceSummary) =
    if place.Published then
        // A guide is a server page, so this is a normal link.
        Html.a [
            prop.key place.Slug
            prop.className "result press"
            prop.href (Route.url [ "slug", place.Slug ] [] Routes.guidePage)
            prop.children [ colourBar place.FamilyDeep; resultText place place.Region ]
        ]
    else
        Html.div [
            prop.key place.Slug
            prop.className "result"
            prop.children [
                colourBar place.FamilyDeep
                resultText place $"{place.Region}, guide coming soon"
            ]
        ]

let private results (model: Model) dispatch =
    match model.Results with
    | None -> Html.none
    | Some Remote.Loading -> Html.none
    | Some(Remote.Failed error) -> States.failed error (fun () -> dispatch Retry)
    | Some(Remote.Loaded []) ->
        Html.p [
            prop.className "no-results"
            prop.text "We don't have a guide for that yet. Try a nearby place or a region."
        ]
    | Some(Remote.Loaded places) -> Html.div [ prop.className "results"; prop.children (places |> List.map result) ]

let private searchBox (model: Model) dispatch =
    Html.div [
        prop.className "home-search"
        prop.children [
            Html.label [
                prop.children [
                    Html.span "Or look up a place"
                    Html.input [
                        prop.className "search-input"
                        prop.type' "search"
                        prop.placeholder "Try Goa, Varanasi or Ladakh"
                        prop.autoComplete "off"
                        prop.value model.Query
                        prop.onChange (QueryChanged >> dispatch)
                    ]
                ]
            ]
            Html.div [
                prop.custom ("aria-live", "polite")
                prop.children [
                    if not model.FromFeeling then
                        results model dispatch
                ]
            ]
        ]
    ]

let private moodTile dispatch (feeling: Feeling) =
    Html.button [
        prop.key feeling.Slug
        prop.className "mood press"
        prop.style [ style.backgroundColor feeling.Colour ]
        prop.onClick (fun _ -> dispatch (ChooseFeeling feeling))
        prop.children [ Html.strong feeling.Name; Html.span (String.concat ", " feeling.Places) ]
    ]

let private moodSection (model: Model) dispatch =
    Html.section [
        prop.className "home-section home-moods"
        prop.children [
            Html.h2 "Or start with a feeling"
            match model.Feelings with
            | Remote.Loading -> Skeletons.feelings "Finding feelings"
            | Remote.Failed error -> States.failed error (fun () -> dispatch LoadFeelings)
            | Remote.Loaded feelings ->
                Html.div [
                    prop.className "moods reveal"
                    prop.children (feelings |> List.map (moodTile dispatch))
                ]
            Html.div [
                prop.custom ("aria-live", "polite")
                prop.children [
                    if model.FromFeeling then
                        results model dispatch
                ]
            ]
        ]
    ]

/// A matchbox label: a framed photo over the place's colour.
let private label (card: LandingCard) (name: string) (line: string) =
    Html.div [
        prop.className "label"
        prop.children [
            Html.div [
                prop.className "label-frame"
                prop.style [ style.borderColor card.Colour ]
                prop.children [
                    Html.img [
                        prop.src card.Photo.Url
                        prop.alt card.Photo.Alt
                        prop.custom ("loading", "lazy")
                    ]
                    Html.div [
                        prop.className "label-text"
                        prop.style [ style.backgroundColor card.Colour ]
                        prop.children [
                            Html.strong name
                            Html.span line
                            Html.small $"{card.Photo.Credit}, {card.Photo.Licence}"
                        ]
                    ]
                ]
            ]
        ]
    ]

let private labelLink (name: LandingCard -> string) (line: LandingCard -> string) (card: LandingCard) =
    // A guide is a server page, so this is a normal link.
    Html.a [
        prop.key card.Title
        prop.className "label-link"
        prop.href (Route.url [ "slug", card.PlaceSlug ] [] Routes.guidePage)
        prop.children [ label card (name card) (line card) ]
    ]

let private fanned (cards: LandingCard list) =
    match cards with
    | right :: left :: middle :: _ -> [ "fan-left", left; "fan-right", right; "fan-middle", middle ]
    | other -> other |> List.map (fun card -> "fan-middle", card)

let private fan (model: Model) =
    Html.div [
        prop.className "fan"
        prop.custom ("aria-hidden", "true")
        prop.children [
            match model.Landing with
            | Remote.Loaded cards ->
                yield!
                    cards
                    |> List.filter (fun card -> not card.Food)
                    |> fanned
                    |> List.map (fun (place, card) ->
                        Html.div [
                            prop.key place
                            prop.className $"fan-card {place}"
                            prop.children [ label card card.Place card.Title ]
                        ])
            | _ ->
                for place in [ "fan-left"; "fan-right"; "fan-middle" ] do
                    Html.div [ prop.key place; prop.className $"fan-card {place} label bone" ]
        ]
    ]

let private visitorNote visitor =
    match visitor with
    | Visitor.Unknown -> ""
    | Visitor.SignedOut -> "Free, and no sign-up needed. Sign in only to keep your trips."
    | Visitor.SignedIn 0 -> "Welcome back."
    | Visitor.SignedIn 1 -> "Welcome back. You have one saved trip."
    | Visitor.SignedIn count -> $"Welcome back. You have {count} saved trips."

let private hero (model: Model) (navigate: string -> unit) =
    Html.section [
        prop.className "home-hero"
        prop.children [
            Html.div [
                prop.className "home-top"
                prop.children [
                    Html.div [ prop.className "home-brand"; prop.text "Namastay" ]
                    match model.Visitor with
                    | Visitor.Unknown -> Html.none
                    | Visitor.SignedOut ->
                        Html.a [
                            prop.className "home-account"
                            prop.href Routes.signInPage.Template
                            prop.text "Sign in"
                        ]
                    | Visitor.SignedIn _ ->
                        Html.a [
                            prop.className "home-account"
                            prop.href Routes.myTripsPage.Template
                            prop.text "My trips"
                        ]
                ]
            ]
            Html.h1 "India is a lot. Find yours."
            fan model
            Html.div [
                prop.className "home-pitch"
                prop.children [
                    Html.p "Swipe through the country, and leave with a trip that's yours, day by day."
                    Html.div [
                        prop.className "home-action"
                        prop.children [
                            Html.button [
                                prop.className "button home-plan press"
                                prop.text "Plan my trip"
                                prop.onClick (fun _ -> navigate Routes.journey.Template)
                            ]
                            Html.span (visitorNote model.Visitor)
                        ]
                    ]
                ]
            ]
        ]
    ]

let private steps = [
    "Swipe what pulls you", "Right for yes, left for no. It takes a couple of minutes."
    "See your taste take shape", "The deck starts wide, then leans towards what you like."
    "Get days that work", "Where you land, how you get between places, and what to do each day."
]

let private howItWorks =
    Html.section [
        prop.className "home-section home-how"
        prop.children [
            Html.h2 "You swipe. We sort out the rest."
            Html.ol [
                prop.children (
                    steps
                    |> List.map (fun (title, text) ->
                        Html.li [ prop.key title; prop.children [ Html.h3 title; Html.p text ] ])
                )
            ]
        ]
    ]

let private cardsOf (model: Model) (keep: LandingCard -> bool) (loaded: LandingCard list -> ReactElement) slots =
    match model.Landing with
    | Remote.Loaded cards -> loaded (cards |> List.filter keep)
    | Remote.Loading ->
        Html.div [
            prop.className "label-grid"
            prop.children [
                for index in 1..slots -> Html.div [ prop.key (string index); prop.className "label bone" ]
            ]
        ]
    | Remote.Failed _ -> Html.none

let private wall (model: Model) dispatch =
    Html.section [
        prop.className "home-section home-wall"
        prop.children [
            Html.div [
                prop.className "wall-top"
                prop.children [ Html.h2 "Sixty places, and no two alike."; searchBox model dispatch ]
            ]
            match model.Landing with
            | Remote.Failed error -> States.failed error (fun () -> dispatch LoadLanding)
            | _ -> Html.none
            cardsOf
                model
                (fun card -> not card.Food)
                (fun cards ->
                    Html.div [
                        prop.className "label-grid"
                        prop.children (cards |> List.map (labelLink _.Place _.Title))
                    ])
                9
        ]
    ]

let private food (model: Model) =
    Html.section [
        prop.className "home-section home-food"
        prop.children [
            Html.div [
                prop.className "food-words"
                prop.children [
                    Html.h2 "Every place eats differently."
                    Html.p "Each guide tells you what to order there, and where people go for it."
                ]
            ]
            cardsOf
                model
                _.Food
                (fun cards ->
                    Html.div [
                        prop.className "label-grid food-grid"
                        prop.children (cards |> List.map (labelLink _.Title _.Place))
                    ])
                3
        ]
    ]

let private close (navigate: string -> unit) =
    Html.section [
        prop.className "home-section home-close"
        prop.children [
            Html.h2 [
                Html.span [ prop.className "tricolour"; prop.text "India" ]
                Html.text " starts with one card."
            ]
            Html.a [
                prop.className "button home-again press"
                prop.href Routes.journey.Template
                prop.text "Plan my trip"
                prop.onClick (fun event ->
                    event.preventDefault ()
                    navigate Routes.journey.Template)
            ]
        ]
    ]

let view (model: Model) dispatch (navigate: string -> unit) =
    Html.div [
        prop.className "home"
        prop.children [
            hero model navigate
            Html.main [
                prop.children [
                    howItWorks
                    wall model dispatch
                    food model
                    moodSection model dispatch
                    close navigate
                ]
            ]
        ]
    ]
