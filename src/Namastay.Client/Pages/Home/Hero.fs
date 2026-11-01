module Namastay.Client.Pages.Home.Hero

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Home.State
open Namastay.Shared
open Namastay.Shared.Contracts

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
            | Remote.Loaded page ->
                yield!
                    page.Cards
                    |> List.filter (fun card -> not card.Food)
                    |> fanned
                    |> List.map (fun (place, card) ->
                        Html.div [
                            prop.key place
                            prop.className $"fan-card {place}"
                            prop.children [ Label.view card.Colour (Some card.Photo) card.Place [ card.Title ] ]
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

let view (model: Model) (navigate: string -> unit) =
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
