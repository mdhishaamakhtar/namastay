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
    | Unsure
    | SignedOut
    | SignedIn of SavedTripSummary list

type Model = {
    Query: string
    /// Counts searches, so a slow old answer can't replace a newer one.
    Attempt: int
    Results: Remote<PlaceSummary list> option
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
    | LoadLanding
    | VisitorKnown of Result<SavedTripSummary list, Client.ClientError>
    | LandingLoaded of Result<LandingPage, Client.ClientError>

let private loadFeelings () =
    Cmd.OfPromise.perform (fun url -> Client.held (Client.get url)) (Route.address Routes.feelings) FeelingsLoaded

let private loadLanding () =
    Cmd.OfPromise.perform (fun url -> Client.held (Client.get url)) (Route.address Routes.landing) LandingLoaded

let private loadVisitor () =
    Cmd.OfPromise.perform Client.get (Route.address Routes.myItineraries) VisitorKnown

let private remote (seed: 'Value option) =
    seed |> Option.map Remote.Loaded |> Option.defaultValue Remote.Loading

let init () =
    let landing = Client.seeded (Route.address Routes.landing)

    {
        Query = ""
        Attempt = 0
        Results = None
        Feelings = remote (Client.seeded (Route.address Routes.feelings))
        Landing = remote landing
        // A saved page shows the landing at once, unless this visitor has their own home.
        Visitor =
            if landing.IsSome && not (Client.wasSignedIn ()) then
                Visitor.Unsure
            else
                Visitor.Unknown
    },
    Cmd.batch [ loadFeelings (); loadLanding (); loadVisitor () ]

let private waitForPause attempt =
    Cmd.OfPromise.perform (fun () -> Promise.sleep searchDelay) () (fun () -> PauseEnded attempt)

let private search (model: Model) =
    let url = Route.at [] [ "query", model.Query.Trim() ] Routes.searchPlaces

    {
        model with
            Results = Some Remote.Loading
    },
    Cmd.OfPromise.perform Client.get url (fun result -> Answered(model.Attempt, result))

let private hasData (remote: Remote<_>) =
    match remote with
    | Remote.Loaded _ -> true
    | _ -> false

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
    | FeelingsLoaded(Error _) when hasData model.Feelings -> model, Cmd.none
    | FeelingsLoaded(Error error) ->
        {
            model with
                Feelings = Remote.Failed error
        },
        Cmd.none
    | VisitorKnown(Ok trips) ->
        Client.rememberSignedIn true

        {
            model with
                Visitor = Visitor.SignedIn trips
        },
        Cmd.none
    | VisitorKnown(Error error) when Client.isSignedOut error ->
        Client.rememberSignedIn false

        {
            model with
                Visitor = Visitor.SignedOut
        },
        Cmd.none
    | VisitorKnown(Error _) -> { model with Visitor = Visitor.Unsure }, Cmd.none
    | LoadLanding -> { model with Landing = Remote.Loading }, loadLanding ()
    | LandingLoaded(Ok page) ->
        {
            model with
                Landing = Remote.Loaded page
        },
        Cmd.none
    | LandingLoaded(Error _) when hasData model.Landing -> model, Cmd.none
    | LandingLoaded(Error error) ->
        {
            model with
                Landing = Remote.Failed error
        },
        Cmd.none
    | PauseEnded _
    | Answered _ -> model, Cmd.none
