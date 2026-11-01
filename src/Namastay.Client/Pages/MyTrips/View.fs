module Namastay.Client.Pages.MyTrips.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.MyTrips.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private row (model: Model) dispatch (trip: SavedTripSummary) =
    let confirming = model.Confirming = Some trip.Id

    Html.li [
        prop.key trip.Id
        prop.className "my-trip"
        prop.children [
            Html.a [
                prop.href (Route.url [ "id", trip.Id ] [] Routes.myTripPage)
                prop.children [ Html.strong trip.Title; Html.small $"{trip.Days} days, saved {trip.SavedOn}" ]
            ]
            Html.button [
                prop.className "my-trip-delete"
                prop.text (if confirming then "Delete this trip?" else "Delete")
                prop.onClick (fun _ -> dispatch (if confirming then Delete trip.Id else AskDelete trip.Id))
            ]
        ]
    ]

let private trips (model: Model) dispatch (saved: SavedTripSummary list) =
    match saved with
    | [] ->
        Html.div [
            prop.className "state"
            prop.children [
                Html.p "You haven't saved a trip yet."
                Html.a [
                    prop.className "button"
                    prop.href Routes.journey.Template
                    prop.text "Plan a trip"
                ]
            ]
        ]
    | items ->
        Html.ul [
            prop.className "my-trips"
            prop.children (items |> List.map (row model dispatch))
        ]

let view (model: Model) dispatch =
    Html.main [
        prop.className "account"
        prop.children [
            Html.a [
                prop.className "account-brand"
                prop.href Routes.home.Template
                prop.text "Namastay"
            ]
            Html.h1 "My trips"
            match model.Trips with
            | Remote.Loading -> States.loading "Finding your trips"
            | Remote.Failed error -> States.failed error (fun () -> dispatch Load)
            | Remote.Loaded saved -> trips model dispatch saved
            match model.Problem with
            | Some text -> Html.p [ prop.className "form-error"; prop.role "alert"; prop.text text ]
            | None -> Html.none
            Html.button [
                prop.className "account-switch"
                prop.text "Sign out"
                prop.onClick (fun _ -> dispatch SignOut)
            ]
        ]
    ]
