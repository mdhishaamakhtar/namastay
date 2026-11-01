/// Home for someone who already knows what Namastay does.
module Namastay.Client.Pages.Home.Signed

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Home.State
open Namastay.Client.Theme
open Namastay.Shared
open Namastay.Shared.Contracts

let private door (title: string) (line: string) (art: string) (colour: string) (href: string) =
    Html.a [
        prop.className "door press"
        prop.style [ style.backgroundColor colour ]
        prop.href href
        prop.children [ Html.img [ prop.src art; prop.alt "" ]; Html.strong title; Html.span line ]
    ]

let private latest (trip: SavedTripSummary) =
    let colour = trip.Colours |> Option.map _.Deep |> Option.defaultValue Theme.ink

    Html.a [
        prop.className "label-link signed-trip"
        prop.href (Route.url [ "id", trip.Id ] [] Routes.myTripPage)
        prop.children [ Label.view colour trip.Photo trip.Title [] ]
    ]

let private tripHeading (trips: SavedTripSummary list) =
    Html.div [
        prop.className "signed-heading"
        prop.children [
            Html.h2 "Your latest trip"
            Html.a [
                prop.href Routes.myTripsPage.Template
                prop.text (
                    if trips.Length = 1 then
                        "All trips"
                    else
                        $"All {trips.Length} trips"
                )
            ]
        ]
    ]

let private nextTrip (model: Model) dispatch (first: bool) =
    let placeCount =
        match model.Landing with
        | Remote.Loaded page -> $"All {page.PlaceCount}, by region"
        | _ -> "By region"

    Html.div [
        prop.className "signed-next"
        prop.children [
            Html.a [
                prop.className "button home-plan press"
                prop.href Routes.journey.Template
                prop.text (if first then "Plan your first trip" else "Plan a new trip")
            ]
            Search.box model dispatch
            Html.div [
                prop.className "doors"
                prop.children [
                    door "Places" placeCount "/feelings/mountains.svg" "#2F5D3A" Routes.explorePage.Template
                    door "Food" "What to eat, and where" "/feelings/food.svg" "#A8201A" Routes.foodPage.Template
                ]
            ]
        ]
    ]

let private main (model: Model) (trips: SavedTripSummary list) dispatch =
    Html.section [
        prop.className (
            if trips.IsEmpty then
                "signed-main signed-first"
            else
                "signed-main"
        )
        prop.children [
            match trips with
            | [] -> nextTrip model dispatch true
            | newest :: _ ->
                tripHeading trips
                latest newest
                nextTrip model dispatch false
        ]
    ]

let view (model: Model) (trips: SavedTripSummary list) dispatch =
    Html.div [
        prop.className "browse"
        prop.children [
            Html.header [
                prop.className "browse-head"
                prop.children [
                    Menu.view Menu.Section.Home
                    Html.div [
                        prop.className "browse-title"
                        prop.children [ Html.h1 "Your India"; Html.p "Welcome back." ]
                    ]
                ]
            ]
            Html.main [
                prop.children [ main model trips dispatch; Sections.moods "Browse by feeling" model dispatch ]
            ]
        ]
    ]
