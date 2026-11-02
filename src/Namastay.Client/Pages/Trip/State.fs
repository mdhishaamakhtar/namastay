module Namastay.Client.Pages.Trip.State

open Browser
open Elmish
open Fable.Core
open Fable.Core.JsInterop
open Fetch
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Theme
open Namastay.Shared
open Namastay.Shared.Contracts

module Navigation = Namastay.Client.Routing.Browser

let private copiedNoteMs = 2000

[<RequireQualifiedAccess>]
type Sharing =
    | Idle
    | Copied
    | Failed

/// A trip is either the public shared one or a signed-in traveller's own copy.
[<RequireQualifiedAccess>]
type Source =
    | Shared of shareToken: string
    | Mine of id: string

type Model = {
    Source: Source
    Trip: Remote<SavedTrip>
    Open: string option
    Sharing: Sharing
    Problem: string option
}

type Msg =
    | Load
    | Loaded of Result<SavedTrip, Client.ClientError>
    | Toggle of string
    | Share
    | Shared of Sharing
    | ClearSharing
    | SaveToAccount
    | SavedToAccount of Result<SavedTripSummary, Client.ClientError>
    | Print

let private tripUrl source =
    match source with
    | Source.Shared token -> Route.at [ "shareToken", token ] [] Routes.getItinerary
    | Source.Mine id -> Route.at [ "id", id ] [] Routes.getMine

let private load source =
    Cmd.OfPromise.perform (fun url -> Client.held (Client.get url)) (tripUrl source) Loaded

let private openFirst (saved: SavedTrip) =
    saved.Trip.Stops |> List.tryHead |> Option.map _.PlaceSlug

let init source =
    {
        Source = source
        Trip = Remote.Loading
        Open = None
        Sharing = Sharing.Idle
        Problem = None
    },
    Cmd.batch [ Cmd.ofEffect (fun _ -> Theme.neutral ()); load source ]

let private applyBase (saved: SavedTrip) =
    Cmd.ofEffect (fun _ -> Theme.ofTrip saved.Trip |> Option.iter Theme.apply)

/// Opens a trip that's just been built, so it needn't be fetched again.
let opened (saved: SavedTrip) =
    {
        Source = Source.Shared saved.ShareToken
        Trip = Remote.Loaded saved
        Open = openFirst saved
        Sharing = Sharing.Idle
        Problem = None
    },
    Cmd.batch [ Cmd.ofEffect (fun _ -> Theme.neutral ()); applyBase saved ]

let private saveToAccount token =
    Cmd.OfPromise.perform
        (fun url -> Client.send url [ RequestProperties.Method HttpMethod.POST ])
        (Route.at [ "shareToken", token ] [] Routes.saveItinerary)
        SavedToAccount

let private copyLink () : JS.Promise<unit> =
    window?navigator?clipboard?writeText(window.location.href)

let private share () =
    Cmd.OfPromise.either copyLink () (fun () -> Shared Sharing.Copied) (fun _ -> Shared Sharing.Failed)

let private clearLater () =
    Cmd.OfPromise.perform (fun () -> Promise.sleep copiedNoteMs) () (fun () -> ClearSharing)

let update msg (model: Model) =
    match msg with
    | Load -> { model with Trip = Remote.Loading }, load model.Source
    | Loaded(Ok saved) ->
        {
            model with
                Trip = Remote.Loaded saved
                Open = openFirst saved
        },
        applyBase saved
    | Loaded(Error error) when Client.isSignedOut error -> model, Cmd.ofEffect (fun _ -> Navigation.signInThenReturn ())
    | Loaded(Error _) when
        (match model.Trip with
         | Remote.Loaded _ -> true
         | _ -> false)
        ->
        model, Cmd.none
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
    | SaveToAccount ->
        match model.Source with
        | Source.Shared token -> { model with Problem = None }, saveToAccount token
        | Source.Mine _ -> model, Cmd.none
    | SavedToAccount(Ok summary) ->
        model, Cmd.ofEffect (fun _ -> Navigation.navigate (Route.url [ "id", summary.Id ] [] Routes.myTripPage))
    | SavedToAccount(Error error) when Client.isSignedOut error ->
        model, Cmd.ofEffect (fun _ -> Navigation.signInThenReturn ())
    | SavedToAccount(Error error) ->
        {
            model with
                Problem = Some(Client.describe error)
        },
        Cmd.none
    | Print -> model, Cmd.ofEffect (fun _ -> window.print ())
