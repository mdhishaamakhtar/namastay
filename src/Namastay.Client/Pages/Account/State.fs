module Namastay.Client.Pages.Account.State

open Elmish
open Fetch
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

type Model = {
    Account: Remote<Account>
    NewPassword: string
    Saved: bool
    Problem: string option
}

type Msg =
    | Load
    | Loaded of Result<Account, Client.ClientError>
    | NewPasswordChanged of string
    | SavePassword
    | PasswordAnswered of Result<Account, Client.ClientError>
    | SignOut
    | SignedOut of Result<unit, Client.ClientError>

let private load () =
    Cmd.OfPromise.perform (fun url -> Client.held (Client.get url)) (Route.address Routes.me) Loaded

let init () =
    {
        Account = Remote.Loading
        NewPassword = ""
        Saved = false
        Problem = None
    },
    load ()

let update msg (model: Model) =
    match msg with
    | Load -> { model with Account = Remote.Loading }, load ()
    | Loaded(Ok account) ->
        {
            model with
                Account = Remote.Loaded account
        },
        Cmd.none
    | Loaded(Error error) when Client.isSignedOut error ->
        Client.rememberSignedIn false
        model, Cmd.ofEffect (fun _ -> Browser.signInThenReturn ())
    | Loaded(Error error) ->
        {
            model with
                Account = Remote.Failed error
        },
        Cmd.none
    | NewPasswordChanged text -> { model with NewPassword = text }, Cmd.none
    | SavePassword ->
        { model with Problem = None },
        Cmd.OfPromise.perform
            (fun password -> Client.post (Route.address Routes.setPassword) { Password = password })
            model.NewPassword
            PasswordAnswered
    | PasswordAnswered(Ok account) ->
        {
            model with
                Account = Remote.Loaded account
                NewPassword = ""
                Saved = true
        },
        Cmd.none
    | PasswordAnswered(Error(Client.ClientError.Server error)) when not error.Fields.IsEmpty ->
        {
            model with
                Problem = Some error.Fields.Head.Message
        },
        Cmd.none
    | PasswordAnswered(Error error) ->
        {
            model with
                Problem = Some(Client.describe error)
        },
        Cmd.none
    | SignOut -> model, Cmd.OfPromise.perform (Client.action HttpMethod.POST) Routes.signOut.Template SignedOut
    | SignedOut(Ok()) ->
        Client.rememberSignedIn false
        model, Cmd.ofEffect (fun _ -> Browser.navigate Routes.home.Template)
    | SignedOut(Error error) ->
        {
            model with
                Problem = Some(Client.describe error)
        },
        Cmd.none
