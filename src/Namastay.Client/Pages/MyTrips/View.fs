module Namastay.Client.Pages.MyTrips.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.MyTrips.State
open Namastay.Client.Theme
open Namastay.Shared
open Namastay.Shared.Contracts

let private label (trip: SavedTripSummary) =
    let colour = trip.Colours |> Option.map _.Deep |> Option.defaultValue Theme.ink

    let route =
        if trip.Route.IsEmpty then
            []
        else
            [ String.concat " → " trip.Route ]

    Html.a [
        prop.className "label-link my-trip-open"
        prop.href (Route.url [ "id", trip.Id ] [] Routes.myTripPage)
        prop.children [ Label.view colour trip.Photo trip.Title route ]
    ]

let private card (model: Model) dispatch (trip: SavedTripSummary) =
    let confirming = model.Confirming = Some trip.Id

    Html.li [
        prop.key trip.Id
        prop.className "my-trip"
        prop.children [
            label trip
            Html.div [
                prop.className "my-trip-foot"
                prop.children [
                    Html.small $"{trip.Days} days. Saved {Dates.readable trip.SavedOn}"
                    Html.button [
                        prop.className "my-trip-delete"
                        prop.text (if confirming then "Delete this trip?" else "Delete")
                        prop.onClick (fun _ -> dispatch (if confirming then Delete trip.Id else AskDelete trip.Id))
                    ]
                ]
            ]
        ]
    ]

let private trips (model: Model) dispatch (saved: SavedTripSummary list) =
    match saved with
    | [] ->
        Html.div [
            prop.className "my-trips-empty reveal"
            prop.children [
                Html.h2 "You haven't saved a trip yet."
                Html.p "Swipe through places you like, and we'll turn them into a trip you can keep and change."
            ]
        ]
    | items ->
        Html.ul [
            prop.className "my-trips reveal"
            prop.children (items |> List.map (card model dispatch))
        ]

let private count (model: Model) =
    match model.Trips with
    | Remote.Loaded [ _ ] -> Html.p [ prop.className "reveal"; prop.text "One trip saved" ]
    | Remote.Loaded(_ :: _ as saved) -> Html.p [ prop.className "reveal"; prop.text $"{saved.Length} trips saved" ]
    | _ -> Html.none

let private header (model: Model) dispatch =
    Html.header [
        prop.className "my-head"
        prop.children [
            Html.div [
                prop.className "home-top"
                prop.children [
                    Html.a [
                        prop.className "home-brand"
                        prop.href Routes.home.Template
                        prop.text "Namastay"
                    ]
                    Html.button [
                        prop.className "account-switch"
                        prop.text "Sign out"
                        prop.onClick (fun _ -> dispatch SignOut)
                    ]
                ]
            ]
            Html.div [
                prop.className "my-title"
                prop.children [
                    Html.div [ prop.children [ Html.h1 "My trips"; count model ] ]
                    Html.a [
                        prop.className "button home-plan press"
                        prop.href Routes.journey.Template
                        prop.text "Plan a new trip"
                    ]
                ]
            ]
        ]
    ]

let view (model: Model) dispatch =
    Html.div [
        prop.className "my-page"
        prop.children [
            header model dispatch
            Html.main [
                prop.children [
                    match model.Trips with
                    | Remote.Loading -> Skeletons.myTrips "Finding your trips"
                    | Remote.Failed error -> States.failed error (fun () -> dispatch Load)
                    | Remote.Loaded saved -> trips model dispatch saved
                    match model.Problem with
                    | Some text -> Html.p [ prop.className "form-error"; prop.role "alert"; prop.text text ]
                    | None -> Html.none
                ]
            ]
        ]
    ]
