module Namastay.Client.Pages.Home.State

open Elmish
open Fable.Core
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Shared
open Namastay.Shared.Contracts

/// Waits for a pause in typing before searching.
let private searchDelay = 200

/// Who's looking, so the page can offer what's useful to them.
[<RequireQualifiedAccess>]
type Visitor =
    | Unknown
    | SignedOut
    | SignedIn of savedTrips: int

type Model = {
    Query: string
    /// Counts searches, so a slow old answer can't replace a newer one.
    Attempt: int
    Results: Remote<PlaceSummary list> option
    /// The feeling behind the results, if any. They appear beside whatever asked for them.
    Chosen: Feeling option
    Feelings: Remote<Feeling list>
    Landing: Remote<LandingPage>
    Visitor: Visitor
}

type Msg =
    | QueryChanged of string
    | PauseEnded of attempt: int
    | Answered of attempt: int * Result<PlaceSummary list, Client.ClientError>
    | Retry
    | LoadFeelings
    | FeelingsLoaded of Result<Feeling list, Client.ClientError>
    | ChooseFeeling of Feeling
    | LoadLanding
    | VisitorKnown of Result<SavedTripSummary list, Client.ClientError>
    | LandingLoaded of Result<LandingPage, Client.ClientError>

let private loadFeelings () =
    Cmd.OfPromise.perform
        (fun url -> Client.held (Client.get<Feeling list> url))
        Routes.feelings.Template
        FeelingsLoaded

let private loadLanding () =
    Cmd.OfPromise.perform (fun url -> Client.held (Client.get<LandingPage> url)) Routes.landing.Template LandingLoaded

let private loadVisitor () =
    Cmd.OfPromise.perform Client.get<SavedTripSummary list> Routes.myItineraries.Template VisitorKnown

let init () =
    {
        Query = ""
        Attempt = 0
        Results = None
        Chosen = None
        Feelings = Remote.Loading
        Landing = Remote.Loading
        Visitor = Visitor.Unknown
    },
    Cmd.batch [ loadFeelings (); loadLanding (); loadVisitor () ]

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

let private chooseFeeling (feeling: Feeling) (model: Model) =
    let attempt = model.Attempt + 1
    let url = Route.url [ "slug", feeling.Slug ] [] Routes.feelingPlaces

    {
        model with
            Query = ""
            Attempt = attempt
            Results = Some Remote.Loading
            Chosen = Some feeling
    },
    Cmd.OfPromise.perform (fun target -> Client.get<PlaceSummary list> target) url (fun result ->
        Answered(attempt, result))

let update msg (model: Model) =
    match msg with
    | QueryChanged query when query.Trim() = "" ->
        {
            model with
                Query = query
                Attempt = model.Attempt + 1
                Results = None
                Chosen = None
        },
        Cmd.none
    | QueryChanged query ->
        let attempt = model.Attempt + 1

        {
            model with
                Query = query
                Attempt = attempt
                Chosen = None
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
    | Retry ->
        match model.Chosen with
        | Some feeling -> chooseFeeling feeling model
        | None -> search model
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
    | VisitorKnown(Ok trips) ->
        {
            model with
                Visitor = Visitor.SignedIn trips.Length
        },
        Cmd.none
    | VisitorKnown(Error error) when Client.isSignedOut error ->
        {
            model with
                Visitor = Visitor.SignedOut
        },
        Cmd.none
    | VisitorKnown(Error _) -> model, Cmd.none
    | LoadLanding -> { model with Landing = Remote.Loading }, loadLanding ()
    | LandingLoaded(Ok page) ->
        {
            model with
                Landing = Remote.Loaded page
        },
        Cmd.none
    | LandingLoaded(Error error) ->
        {
            model with
                Landing = Remote.Failed error
        },
        Cmd.none
    | ChooseFeeling feeling -> chooseFeeling feeling model
    | PauseEnded _
    | Answered _ -> model, Cmd.none
