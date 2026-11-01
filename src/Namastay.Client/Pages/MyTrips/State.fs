module Namastay.Client.Pages.MyTrips.State

open Elmish
open Fetch
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

type Model = {
    Trips: Remote<SavedTripSummary list>
    /// A trip waiting for a second press before it's deleted.
    Confirming: string option
    Problem: string option
}

type Msg =
    | Load
    | Loaded of Result<SavedTripSummary list, Client.ClientError>
    | AskDelete of string
    | Delete of string
    | Deleted of Result<unit, Client.ClientError>
    | SignOut
    | SignedOut of Result<unit, Client.ClientError>

let private load () =
    Cmd.OfPromise.perform (fun url -> Client.get<SavedTripSummary list> url) Routes.myItineraries.Template Loaded

let init () =
    {
        Trips = Remote.Loading
        Confirming = None
        Problem = None
    },
    load ()

let update msg (model: Model) =
    match msg with
    | Load -> { model with Trips = Remote.Loading }, load ()
    | Loaded(Ok trips) ->
        {
            model with
                Trips = Remote.Loaded trips
        },
        Cmd.none
    | Loaded(Error error) when Client.isSignedOut error -> model, Cmd.ofEffect (fun _ -> Browser.signInThenReturn ())
    | Loaded(Error error) ->
        {
            model with
                Trips = Remote.Failed error
        },
        Cmd.none
    | AskDelete id -> { model with Confirming = Some id }, Cmd.none
    | Delete id ->
        { model with Confirming = None },
        Cmd.OfPromise.perform (Client.action HttpMethod.DELETE) (Route.url [ "id", id ] [] Routes.deleteMine) Deleted
    | Deleted(Ok()) -> { model with Problem = None }, load ()
    | Deleted(Error error) ->
        {
            model with
                Problem = Some(Client.describe error)
        },
        Cmd.none
    | SignOut -> model, Cmd.OfPromise.perform (Client.action HttpMethod.POST) Routes.signOut.Template SignedOut
    | SignedOut(Ok()) -> model, Cmd.ofEffect (fun _ -> Browser.navigate Routes.home.Template)
    | SignedOut(Error error) ->
        {
            model with
                Problem = Some(Client.describe error)
        },
        Cmd.none
