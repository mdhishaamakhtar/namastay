module Namastay.Client.Pages.Trip.State

open Elmish
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Shared
open Namastay.Shared.Contracts

type Model = {
    ShareToken: string
    Trip: Remote<SavedTrip>
}

type Msg =
    | Load
    | Loaded of Result<SavedTrip, Client.ClientError>

let private load (token: string) =
    Cmd.OfPromise.perform
        (fun url -> Client.get<SavedTrip> url)
        (Route.url [ "shareToken", token ] [] Routes.getItinerary)
        Loaded

let init token =
    {
        ShareToken = token
        Trip = Remote.Loading
    },
    load token

let update msg (model: Model) =
    match msg with
    | Load -> { model with Trip = Remote.Loading }, load model.ShareToken
    | Loaded(Ok saved) ->
        {
            model with
                Trip = Remote.Loaded saved
        },
        Cmd.none
    | Loaded(Error error) ->
        {
            model with
                Trip = Remote.Failed error
        },
        Cmd.none
