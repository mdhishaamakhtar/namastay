module Namastay.Client.Pages.Trip.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Trip.State

let view (model: Model) dispatch =
    Html.main [
        match model.Trip with
        | Remote.Loading -> States.loading "Finding your trip"
        | Remote.Failed error -> States.failed error (fun () -> dispatch Load)
        | Remote.Loaded saved ->
            Html.h1 saved.Trip.Title

            Html.ol [
                for stop in saved.Trip.Stops do
                    Html.li [ prop.key stop.PlaceSlug; prop.text $"{stop.Name}, {stop.Days} days" ]
            ]
    ]
