module Namastay.Client.Pages.SignIn.State

open Elmish
open Namastay.Client.Api
open Namastay.Client.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type Mode =
    | SignIn
    | SignUp

type Model = {
    Mode: Mode
    Email: string
    Password: string
    Working: bool
    Google: bool
    Photo: GuidePhoto option
    Error: string option
    /// This email belongs to a Google account, so the page points there.
    UseGoogle: bool
    Fields: FieldError list
}

type Msg =
    | OptionsLoaded of Result<SignInOptions, Client.ClientError>
    | SwitchMode
    | EmailChanged of string
    | PasswordChanged of string
    | Submit
    | Answered of Result<Account, Client.ClientError>

let init () =
    {
        Mode = Mode.SignIn
        Email = ""
        Password = ""
        Working = false
        Google = false
        Photo = None
        Error = None
        UseGoogle = false
        Fields = []
    },
    Cmd.OfPromise.perform (fun url -> Client.get<SignInOptions> url) Routes.signInOptions.Template OptionsLoaded

let private submit (model: Model) =
    let credentials = {
        Email = model.Email
        Password = model.Password
    }

    let url =
        match model.Mode with
        | Mode.SignIn -> Routes.signIn.Template
        | Mode.SignUp -> Routes.signUp.Template

    Cmd.OfPromise.perform (fun body -> Client.post<Credentials, Account> url body) credentials Answered

let update msg (model: Model) =
    match msg with
    | OptionsLoaded(Ok options) ->
        {
            model with
                Google = options.Google
                Photo = options.Photo
        },
        Cmd.none
    | OptionsLoaded(Error _) -> model, Cmd.none
    | SwitchMode ->
        {
            model with
                Mode =
                    if model.Mode = Mode.SignIn then
                        Mode.SignUp
                    else
                        Mode.SignIn
                Error = None
                UseGoogle = false
                Fields = []
        },
        Cmd.none
    | EmailChanged text -> { model with Email = text }, Cmd.none
    | PasswordChanged text -> { model with Password = text }, Cmd.none
    | Submit when model.Working -> model, Cmd.none
    | Submit ->
        {
            model with
                Working = true
                Error = None
                UseGoogle = false
                Fields = []
        },
        submit model
    | Answered(Ok _) ->
        { model with Working = false },
        Cmd.ofEffect (fun _ -> Browser.navigate (Browser.afterSignIn Routes.home.Template))
    | Answered(Error(Client.ClientError.Server error)) when not error.Fields.IsEmpty ->
        {
            model with
                Working = false
                Fields = error.Fields
        },
        Cmd.none
    | Answered(Error(Client.ClientError.Server error)) when error.Code = ErrorCode.UseGoogle ->
        {
            model with
                Working = false
                UseGoogle = true
                Error = Some error.Message
        },
        Cmd.none
    | Answered(Error error) ->
        {
            model with
                Working = false
                Error = Some(Client.describe error)
        },
        Cmd.none
