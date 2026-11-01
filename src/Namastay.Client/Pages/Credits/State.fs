module Namastay.Client.Pages.Credits.State

open Elmish
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Shared
open Namastay.Shared.Contracts

type Model = { Credits: Remote<PhotoCredit list> }

type Msg =
    | Load
    | Loaded of Result<PhotoCredit list, Client.ClientError>

let private load () =
    Cmd.OfPromise.perform Client.get<PhotoCredit list> Routes.credits.Template Loaded

let init () =
    match Client.seeded<PhotoCredit list> Routes.credits.Template with
    | Some credits -> { Credits = Remote.Loaded credits }, Cmd.none
    | None -> { Credits = Remote.Loading }, load ()

let update msg (_: Model) =
    match msg with
    | Load -> { Credits = Remote.Loading }, load ()
    | Loaded(Ok credits) -> { Credits = Remote.Loaded credits }, Cmd.none
    | Loaded(Error error) -> { Credits = Remote.Failed error }, Cmd.none
