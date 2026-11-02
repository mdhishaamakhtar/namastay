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
    | Eat of Diet

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
    Diet: Diet
}

type Model = {
    Graph: Remote<JourneyGraph>
    Queue: string list
    Answers: CardAnswer list
    Plan: Plan
    Step: Step
    /// Questions before this one have been answered, so going back can show those answers.
    Furthest: int
    /// What this trip is limited to, and how to say so.
    Scope: TripScope option
    ScopeName: string option
}

type Msg =
    | LoadGraph
    | GraphLoaded of Result<JourneyGraph, Client.ClientError>
    | Decided of liked: bool
    | Undone
    | Chose of Choice
    | WentBack
    | FinishEarly
    | BuildTrip
    | TripBuilt of Result<SavedTrip, Client.ClientError>

let questionCount = 6

let private scopeInAddress () =
    let kind =
        match Browser.queryParam "scope" with
        | Some "region" -> Some ScopeKind.Region
        | Some "food" -> Some ScopeKind.FoodInRegion
        | Some "feeling" -> Some ScopeKind.Feeling
        | Some "dish" -> Some ScopeKind.Dish
        | _ -> None

    match kind, Browser.queryParam "of" with
    | Some chosen, Some slug -> Some { Kind = chosen; Slug = slug }
    | _ -> None

let private loadGraph () =
    Cmd.OfPromise.perform
        (fun url -> Client.held (Client.get url))
        {
            Route.address Routes.journeyStart with
                Url = Routes.journeyStart.Template + window.location.search
        }
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
            Diet = Diet.Everything
        }
        Step = Step.Swiping
        Furthest = 0
        Scope = scopeInAddress ()
        ScopeName = Browser.queryParam "label"
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
            image.src <- Client.fromApi url))

let private settleStage (graph: JourneyGraph) answers =
    Cmd.ofEffect (fun _ -> Theme.stage (JourneyPath.leadingColours graph answers))

let private tripRequest (model: Model) = {
    Answers = model.Answers
    Days = model.Plan.Days
    Month = model.Plan.Month
    Travellers = model.Plan.Travellers
    Budget = model.Plan.Budget
    TransportComfort = model.Plan.Comfort
    Scope = model.Scope
    Diet = Some model.Plan.Diet
}

let private build (model: Model) =
    { model with Step = Step.Building },
    Cmd.batch [
        Cmd.ofEffect (fun _ -> Theme.neutral ())
        Cmd.OfPromise.perform
            (fun request -> Client.held (Client.post (Route.address Routes.createItinerary) request))
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
    | Choice.Eat diet -> { plan with Diet = diet }

let isChosen (plan: Plan) choice =
    match choice with
    | Choice.Month month -> plan.Month = month
    | Choice.Days days -> plan.Days = days
    | Choice.Who who -> plan.Travellers = who
    | Choice.Spend budget -> plan.Budget = budget
    | Choice.Travel comfort -> plan.Comfort = comfort
    | Choice.Eat diet -> plan.Diet = diet

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

/// Takes back the last answer, so its card can be decided again.
let private undo (graph: JourneyGraph) (model: Model) =
    match List.rev model.Answers with
    | [] -> model, Cmd.none
    | last :: earlier ->
        let answers = List.rev earlier

        {
            model with
                Answers = answers
                Queue = [ last.Card ]
                Step = Step.Swiping
        },
        settleStage graph answers

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
    | Undone, Remote.Loaded graph, Step.Swiping -> undo graph model
    | FinishEarly, _, Step.Swiping when not model.Answers.IsEmpty -> { model with Step = Step.Asking 0 }, Cmd.none
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
                    Furthest = max model.Furthest (index + 1)
            },
            Cmd.none
    // A visitor who stopped early still has a card waiting, so they go back to that one.
    | WentBack, _, Step.Asking 0 when not model.Queue.IsEmpty -> { model with Step = Step.Swiping }, Cmd.none
    | WentBack, Remote.Loaded graph, Step.Asking 0 -> undo graph model
    | WentBack, _, Step.Asking index ->
        {
            model with
                Step = Step.Asking(index - 1)
        },
        Cmd.none
    | BuildTrip, _, _ -> build model
    // App.fs takes a finished trip straight to its page.
    | TripBuilt(Ok _), _, _ -> model, Cmd.none
    | TripBuilt(Error error), _, _ -> { model with Step = Step.Failed error }, Cmd.none
    | Decided _, _, _
    | Undone, _, _
    | FinishEarly, _, _
    | Chose _, _, _
    | WentBack, _, _ -> model, Cmd.none
