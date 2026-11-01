module Namastay.Client.Pages.MyTrips.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.MyTrips.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private photo (trip: SavedTripSummary) =
    match trip.Photo with
    | Some item ->
        Html.figure [
            prop.className "my-trip-photo"
            prop.children [
                Html.img [ prop.src item.Url; prop.alt item.Alt; prop.custom ("loading", "lazy") ]
                Html.figcaption [ prop.text $"{item.Credit}, {item.Licence}" ]
            ]
        ]
    | None -> Html.div [ prop.className "my-trip-photo my-trip-no-photo" ]

let private card (model: Model) dispatch (trip: SavedTripSummary) =
    let confirming = model.Confirming = Some trip.Id

    let frame =
        match trip.Colours with
        | Some colours -> [ style.backgroundColor colours.Deep; style.color colours.TextOnDeep ]
        | None -> []

    Html.li [
        prop.key trip.Id
        prop.className "my-trip"
        prop.style frame
        prop.children [
            Html.a [
                prop.className "my-trip-open press"
                prop.href (Route.url [ "id", trip.Id ] [] Routes.myTripPage)
                prop.children [
                    photo trip
                    Html.h2 trip.Title
                    if not trip.Route.IsEmpty then
                        Html.p [ prop.className "my-trip-route"; prop.text (String.concat " → " trip.Route) ]
                ]
            ]
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
            prop.className "my-trips-empty"
            prop.children [
                Html.h2 "You haven't saved a trip yet."
                Html.p "Swipe through places you like, and we'll turn them into a trip you can keep and change."
            ]
        ]
    | items ->
        Html.ul [
            prop.className "my-trips"
            prop.children (items |> List.map (card model dispatch))
        ]

let private count (model: Model) =
    match model.Trips with
    | Remote.Loaded [ _ ] -> "One trip saved"
    | Remote.Loaded(_ :: _ as saved) -> $"{saved.Length} trips saved"
    | _ -> "Your saved trips live here"

let private panel (model: Model) dispatch =
    Html.aside [
        prop.className "trip-panel"
        prop.children [
            Html.a [
                prop.className "journey-brand"
                prop.href Routes.home.Template
                prop.text "Namastay"
            ]
            Html.h1 "My trips"
            Html.p (count model)
            Html.a [
                prop.className "button trip-share press"
                prop.href Routes.journey.Template
                prop.text "Plan a new trip"
            ]
            Html.button [
                prop.className "account-switch"
                prop.text "Sign out"
                prop.onClick (fun _ -> dispatch SignOut)
            ]
        ]
    ]

let view (model: Model) dispatch =
    Html.div [
        prop.className "trip"
        prop.children [
            panel model dispatch
            Html.div [
                prop.className "trip-content"
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
