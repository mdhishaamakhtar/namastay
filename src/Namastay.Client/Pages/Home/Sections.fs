module Namastay.Client.Pages.Home.Sections

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.Home.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private moodTile (feeling: Feeling) =
    Html.a [
        prop.key feeling.Slug
        prop.className "mood press"
        prop.style [ style.backgroundColor feeling.Colour ]
        prop.href (Route.url [ "slug", feeling.Slug ] [] Routes.feelingPage)
        prop.children [
            Html.img [ prop.src $"/feelings/{feeling.Slug}.svg"; prop.alt "" ]
            Html.strong feeling.Name
            Html.span (String.concat ", " feeling.Places)
        ]
    ]

let moods (heading: string) (model: Model) dispatch =
    Html.section [
        prop.className "home-section home-moods"
        prop.children [
            Html.h2 heading
            match model.Feelings with
            | Remote.Loading -> Skeletons.feelings "Finding feelings"
            | Remote.Failed error -> States.failed error (fun () -> dispatch LoadFeelings)
            | Remote.Loaded feelings ->
                Html.div [ prop.className "moods reveal"; prop.children (feelings |> List.map moodTile) ]
        ]
    ]

let private labelLink (name: LandingCard -> string) (line: LandingCard -> string) (card: LandingCard) =
    // A guide is a server page, so this is a normal link.
    Html.a [
        prop.key card.Title
        prop.className "label-link"
        prop.href (Route.url [ "slug", card.PlaceSlug ] [] Routes.guidePage)
        prop.children [ Label.view card.Colour (Some card.Photo) (name card) [ line card ] ]
    ]

let private steps = [
    "Swipe what pulls you", "Right for yes, left for no. It takes a couple of minutes."
    "See your taste take shape", "The deck starts wide, then leans towards what you like."
    "Get days that work", "Where you land, how you get between places, and what to do each day."
]

let howItWorks =
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
    | Remote.Loaded page -> loaded (page.Cards |> List.filter keep)
    | Remote.Loading ->
        Html.div [
            prop.className "label-grid home-wall-grid"
            prop.children [
                for index in 1..slots -> Html.div [ prop.key (string index); prop.className "label bone" ]
            ]
        ]
    | Remote.Failed _ -> Html.none

let private wallHeading (model: Model) =
    match model.Landing with
    | Remote.Loaded page -> Html.h2 $"{page.PlaceCount} places, and no two alike."
    | _ -> Html.h2 [ prop.className "bone bone-heading" ]

let wall (model: Model) dispatch =
    Html.section [
        prop.className "home-section home-wall"
        prop.children [
            Html.div [
                prop.className "wall-top"
                prop.children [ wallHeading model; Search.searchBox model dispatch ]
            ]
            match model.Landing with
            | Remote.Failed error -> States.failed error (fun () -> dispatch LoadLanding)
            | _ -> Html.none
            cardsOf
                model
                (fun card -> not card.Food)
                (fun cards ->
                    Html.div [
                        prop.className "label-grid home-wall-grid"
                        prop.children (cards |> List.map (labelLink _.Place _.Title))
                    ])
                9
        ]
    ]

let food (model: Model) =
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

let close (navigate: string -> unit) =
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
