module Namastay.Client.Pages.Journey.State

open Elmish
open Browser
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Routing
open Namastay.Client.Theme
open Namastay.Shared
open Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type Choice =
    | Month of int option
    | Days of int
    | Who of TravellerType
    | Spend of BudgetLevel
    | Travel of TransportComfort

[<RequireQualifiedAccess>]
type Step =
    | Swiping
    | Asking of index: int
    | Building
    | Failed of Client.ClientError

type Plan = {
    Month: int option
    Days: int
    Travellers: TravellerType
    Budget: BudgetLevel
    Comfort: TransportComfort
}

type Model = {
    Graph: Remote<JourneyGraph>
    Queue: string list
    Answers: CardAnswer list
    Plan: Plan
    Step: Step
}

type Msg =
    | LoadGraph
    | GraphLoaded of Result<JourneyGraph, Client.ClientError>
    | Decided of liked: bool
    | Chose of Choice
    | BuildTrip
    | TripBuilt of Result<SavedTrip, Client.ClientError>

let questionCount = 5

let private loadGraph () =
    Cmd.OfPromise.perform
        (fun url -> Client.held (Client.get<JourneyGraph> url))
        Routes.journeyStart.Template
        GraphLoaded

let init () =
    {
        Graph = Remote.Loading
        Queue = []
        Answers = []
        Plan = {
            Month = None
            Days = 14
            Travellers = TravellerType.Solo
            Budget = BudgetLevel.Mid
            Comfort = TransportComfort.Anything
        }
        Step = Step.Swiping
    },
    Cmd.batch [ Cmd.ofEffect (fun _ -> Theme.stage None); loadGraph () ]

/// Decoding the next image first means a card never appears half loaded.
let private preload (graph: JourneyGraph) (queue: string list) =
    Cmd.ofEffect (fun _ ->
        queue
        |> List.tryHead
        |> Option.bind (fun slug -> graph.Cards |> List.tryFind (fun card -> card.Slug = slug))
        |> Option.bind _.ImageUrl
        |> Option.iter (fun url ->
            let image = document.createElement "img" :?> Types.HTMLImageElement
            image.src <- url))

let private settleStage (graph: JourneyGraph) answers =
    Cmd.ofEffect (fun _ -> Theme.stage (JourneyPath.leadingColours graph answers))

let private tripRequest (model: Model) = {
    Answers = model.Answers
    Days = model.Plan.Days
    Month = model.Plan.Month
    Travellers = model.Plan.Travellers
    Budget = model.Plan.Budget
    TransportComfort = model.Plan.Comfort
}

let private build (model: Model) =
    { model with Step = Step.Building },
    Cmd.batch [
        Cmd.ofEffect (fun _ -> Theme.neutral ())
        Cmd.OfPromise.perform
            (fun request -> Client.held (Client.post<TripRequest, SavedTrip> Routes.createItinerary.Template request))
            (tripRequest model)
            TripBuilt
    ]

let private apply choice (plan: Plan) =
    match choice with
    | Choice.Month month -> { plan with Month = month }
    | Choice.Days days -> { plan with Days = days }
    | Choice.Who who -> { plan with Travellers = who }
    | Choice.Spend budget -> { plan with Budget = budget }
    | Choice.Travel comfort -> { plan with Comfort = comfort }

let private decide (graph: JourneyGraph) liked (model: Model) =
    let slug = List.head model.Queue
    let answers = model.Answers @ [ { Card = slug; Liked = liked } ]
    let queue = JourneyPath.next graph answers |> Option.toList

    let step = if queue.IsEmpty then Step.Asking 0 else Step.Swiping

    {
        model with
            Answers = answers
            Queue = queue
            Step = step
    },
    Cmd.batch [ settleStage graph answers; preload graph queue ]

let update msg (model: Model) =
    match msg, model.Graph, model.Step with
    | LoadGraph, _, _ -> { model with Graph = Remote.Loading }, loadGraph ()
    | GraphLoaded(Ok graph), _, _ ->
        let queue = JourneyPath.next graph [] |> Option.toList

        {
            model with
                Graph = Remote.Loaded graph
                Queue = queue
        },
        preload graph (List.truncate 2 queue)
    | GraphLoaded(Error error), _, _ ->
        {
            model with
                Graph = Remote.Failed error
        },
        Cmd.none
    | Decided liked, Remote.Loaded graph, Step.Swiping when not model.Queue.IsEmpty -> decide graph liked model
    | Chose choice, _, Step.Asking index ->
        let chosen = {
            model with
                Plan = apply choice model.Plan
        }

        if index + 1 = questionCount then
            build chosen
        else
            {
                chosen with
                    Step = Step.Asking(index + 1)
            },
            Cmd.none
    | BuildTrip, _, _ -> build model
    | TripBuilt(Ok saved), _, _ ->
        model, Cmd.ofEffect (fun _ -> Browser.navigate (Route.url [ "shareToken", saved.ShareToken ] [] Routes.trip))
    | TripBuilt(Error error), _, _ -> { model with Step = Step.Failed error }, Cmd.none
    | Decided _, _, _
    | Chose _, _, _ -> model, Cmd.none
