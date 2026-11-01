module Namastay.Client.Pages.Home.State

open Elmish
open Fable.Core
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Shared
open Namastay.Shared.Contracts

/// Waits for a pause in typing before searching.
let private searchDelay = 200

type Model = {
    Query: string
    /// Counts searches, so a slow old answer can't replace a newer one.
    Attempt: int
    Results: Remote<PlaceSummary list> option
    Feelings: Remote<Feeling list>
}

type Msg =
    | QueryChanged of string
    | PauseEnded of attempt: int
    | Answered of attempt: int * Result<PlaceSummary list, Client.ClientError>
    | Retry
    | LoadFeelings
    | FeelingsLoaded of Result<Feeling list, Client.ClientError>
    | ChooseFeeling of Feeling

let private loadFeelings () =
    Cmd.OfPromise.perform
        (fun url -> Client.held (Client.get<Feeling list> url))
        Routes.feelings.Template
        FeelingsLoaded

let init () =
    {
        Query = ""
        Attempt = 0
        Results = None
        Feelings = Remote.Loading
    },
    loadFeelings ()

let private waitForPause attempt =
    Cmd.OfPromise.perform (fun () -> Promise.sleep searchDelay) () (fun () -> PauseEnded attempt)

let private search (model: Model) =
    let url = Route.url [] [ "query", model.Query.Trim() ] Routes.searchPlaces

    {
        model with
            Results = Some Remote.Loading
    },
    Cmd.OfPromise.perform (fun target -> Client.get<PlaceSummary list> target) url (fun result ->
        Answered(model.Attempt, result))

let update msg (model: Model) =
    match msg with
    | QueryChanged query when query.Trim() = "" ->
        {
            model with
                Query = query
                Attempt = model.Attempt + 1
                Results = None
        },
        Cmd.none
    | QueryChanged query ->
        let attempt = model.Attempt + 1

        {
            model with
                Query = query
                Attempt = attempt
        },
        waitForPause attempt
    | PauseEnded attempt when attempt = model.Attempt -> search model
    | Answered(attempt, Ok places) when attempt = model.Attempt ->
        {
            model with
                Results = Some(Remote.Loaded places)
        },
        Cmd.none
    | Answered(attempt, Error error) when attempt = model.Attempt ->
        {
            model with
                Results = Some(Remote.Failed error)
        },
        Cmd.none
    | Retry -> search model
    | LoadFeelings -> { model with Feelings = Remote.Loading }, loadFeelings ()
    | FeelingsLoaded(Ok feelings) ->
        {
            model with
                Feelings = Remote.Loaded feelings
        },
        Cmd.none
    | FeelingsLoaded(Error error) ->
        {
            model with
                Feelings = Remote.Failed error
        },
        Cmd.none
    | ChooseFeeling feeling ->
        let attempt = model.Attempt + 1
        let url = Route.url [ "slug", feeling.Slug ] [] Routes.feelingPlaces

        {
            model with
                Query = ""
                Attempt = attempt
                Results = Some Remote.Loading
        },
        Cmd.OfPromise.perform (fun target -> Client.get<PlaceSummary list> target) url (fun result ->
            Answered(attempt, result))
    | PauseEnded _
    | Answered _ -> model, Cmd.none
