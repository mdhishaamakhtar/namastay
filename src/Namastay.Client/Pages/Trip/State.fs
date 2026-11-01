module Namastay.Client.Pages.Trip.State

open Browser
open Elmish
open Fable.Core
open Fable.Core.JsInterop
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Theme
open Namastay.Shared
open Namastay.Shared.Contracts

let private copiedNoteMs = 2000

[<RequireQualifiedAccess>]
type Sharing =
    | Idle
    | Copied
    | Failed

type Model = {
    ShareToken: string
    Trip: Remote<SavedTrip>
    Open: string option
    Sharing: Sharing
}

type Msg =
    | Load
    | Loaded of Result<SavedTrip, Client.ClientError>
    | Toggle of string
    | Share
    | Shared of Sharing
    | ClearSharing

let private load (token: string) =
    Cmd.OfPromise.perform
        (fun url -> Client.get<SavedTrip> url)
        (Route.url [ "shareToken", token ] [] Routes.getItinerary)
        Loaded

let init token =
    {
        ShareToken = token
        Trip = Remote.Loading
        Open = None
        Sharing = Sharing.Idle
    },
    load token

let private applyBase (trip: Trip) =
    Cmd.ofEffect (fun _ ->
        trip.Stops
        |> List.tryFind (fun stop -> stop.FamilySlug = trip.BaseFamily)
        |> Option.iter (fun stop -> Theme.apply stop.Colours))

let private copyLink () : JS.Promise<unit> =
    window?navigator?clipboard?writeText(window.location.href)

let private share () =
    Cmd.OfPromise.either copyLink () (fun () -> Shared Sharing.Copied) (fun _ -> Shared Sharing.Failed)

let private clearLater () =
    Cmd.OfPromise.perform (fun () -> Promise.sleep copiedNoteMs) () (fun () -> ClearSharing)

let update msg (model: Model) =
    match msg with
    | Load -> { model with Trip = Remote.Loading }, load model.ShareToken
    | Loaded(Ok saved) ->
        {
            model with
                Trip = Remote.Loaded saved
                Open = saved.Trip.Stops |> List.tryHead |> Option.map _.PlaceSlug
        },
        applyBase saved.Trip
    | Loaded(Error error) ->
        {
            model with
                Trip = Remote.Failed error
        },
        Cmd.none
    | Toggle slug ->
        {
            model with
                Open = if model.Open = Some slug then None else Some slug
        },
        Cmd.none
    | Share -> model, share ()
    | Shared result -> { model with Sharing = result }, clearLater ()
    | ClearSharing -> { model with Sharing = Sharing.Idle }, Cmd.none
