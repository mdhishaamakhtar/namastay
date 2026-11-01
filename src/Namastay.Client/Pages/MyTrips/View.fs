module Namastay.Client.Pages.MyTrips.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.MyTrips.State
open Namastay.Client.Theme
open Namastay.Shared
open Namastay.Shared.Contracts

let private label (trip: SavedTripSummary) =
    let colour = trip.Colours |> Option.map _.Deep |> Option.defaultValue Theme.ink

    Html.a [
        prop.className "label-link my-trip-open"
        prop.href (Route.url [ "id", trip.Id ] [] Routes.myTripPage)
        prop.children [ Label.view colour trip.Photo trip.Title [] ]
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
    | [] -> Html.none
    | items ->
        Html.ul [
            prop.className "my-trips reveal"
            prop.children (items |> List.map (card model dispatch))
        ]

let private count (model: Model) =
    match model.Trips with
    | Remote.Loaded [] -> Html.p [ prop.className "reveal"; prop.text "No trips yet" ]
    | Remote.Loaded [ _ ] -> Html.p [ prop.className "reveal"; prop.text "One trip saved" ]
    | Remote.Loaded(_ :: _ as saved) -> Html.p [ prop.className "reveal"; prop.text $"{saved.Length} trips saved" ]
    | _ -> Html.none

let private header (model: Model) dispatch =
    Html.header [
        prop.className "browse-head"
        prop.children [
            Menu.view Menu.Section.MyTrips
            Html.div [
                prop.className "browse-title"
                prop.children [
                    Html.h1 "My trips"
                    count model
                    Html.div [
                        prop.className "browse-plan my-actions"
                        prop.children [
                            Html.a [
                                prop.className "button press"
                                prop.href Routes.journey.Template
                                prop.text (
                                    match model.Trips with
                                    | Remote.Loaded [] -> "Plan your first trip"
                                    | _ -> "Plan a new trip"
                                )
                            ]
                            Html.button [
                                prop.className "account-switch"
                                prop.text "Sign out"
                                prop.onClick (fun _ -> dispatch SignOut)
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private addPassword (model: Model) dispatch =
    Html.form [
        prop.className "my-password"
        prop.onSubmit (fun event ->
            event.preventDefault ()
            dispatch SavePassword)
        prop.children [
            Html.h2 "Add a password"
            Html.p "You sign in with Google. Add a password and you can use your email too."
            Html.input [
                prop.className "search-input"
                prop.type' "password"
                prop.ariaLabel "New password"
                prop.placeholder "New password, 10 characters or more"
                prop.custom ("autoComplete", "new-password")
                prop.value model.NewPassword
                prop.onChange (NewPasswordChanged >> dispatch)
            ]
            match model.PasswordProblem with
            | Some text -> Html.p [ prop.className "form-error"; prop.role "alert"; prop.text text ]
            | None -> Html.none
            Html.button [ prop.className "button press"; prop.type'.submit; prop.text "Save password" ]
        ]
    ]

let private password (model: Model) dispatch =
    match model.Account with
    | Some account when not account.HasPassword -> addPassword model dispatch
    | Some _ when model.PasswordSaved -> Html.p [ prop.className "my-password-saved"; prop.text "Password saved." ]
    | _ -> Html.none

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
                    password model dispatch
                ]
            ]
        ]
    ]
