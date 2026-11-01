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
    Account: Account option
    NewPassword: string
    PasswordSaved: bool
    PasswordProblem: string option
}

type Msg =
    | Load
    | Loaded of Result<SavedTripSummary list, Client.ClientError>
    | AskDelete of string
    | Delete of string
    | Deleted of Result<unit, Client.ClientError>
    | SignOut
    | SignedOut of Result<unit, Client.ClientError>
    | AccountLoaded of Result<Account, Client.ClientError>
    | NewPasswordChanged of string
    | SavePassword
    | PasswordAnswered of Result<Account, Client.ClientError>

let private load () =
    Cmd.OfPromise.perform
        (fun url -> Client.held (Client.get<SavedTripSummary list> url))
        Routes.myItineraries.Template
        Loaded

let init () =
    {
        Trips = Remote.Loading
        Confirming = None
        Problem = None
        Account = None
        NewPassword = ""
        PasswordSaved = false
        PasswordProblem = None
    },
    Cmd.batch [
        load ()
        Cmd.OfPromise.perform Client.get<Account> Routes.me.Template AccountLoaded
    ]

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
    | AccountLoaded(Ok account) -> { model with Account = Some account }, Cmd.none
    | AccountLoaded(Error _) -> model, Cmd.none
    | NewPasswordChanged text -> { model with NewPassword = text }, Cmd.none
    | SavePassword ->
        { model with PasswordProblem = None },
        Cmd.OfPromise.perform
            (fun password -> Client.post<NewPassword, Account> Routes.setPassword.Template { Password = password })
            model.NewPassword
            PasswordAnswered
    | PasswordAnswered(Ok account) ->
        {
            model with
                Account = Some account
                NewPassword = ""
                PasswordSaved = true
        },
        Cmd.none
    | PasswordAnswered(Error(Client.ClientError.Server error)) when not error.Fields.IsEmpty ->
        {
            model with
                PasswordProblem = Some error.Fields.Head.Message
        },
        Cmd.none
    | PasswordAnswered(Error error) ->
        {
            model with
                PasswordProblem = Some(Client.describe error)
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
