module Namastay.Client.Pages.Journey.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Journey
open Namastay.Client.Pages.Journey.State
open Namastay.Client.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

let private shell (progress: string) (children: ReactElement list) =
    Html.div [
        prop.className "journey"
        prop.children [
            Html.div [
                prop.className "journey-top"
                prop.children [ Menu.brand; Html.span progress ]
            ]
            match Browser.queryParam "label" with
            | Some name ->
                Html.p [
                    prop.className "journey-limit"
                    prop.children [
                        Html.span [ prop.className "journey-scope"; prop.text $"{name} only" ]
                        Html.span "Every card and stop stays inside this."
                    ]
                ]
            | None -> Html.none
            yield! children
        ]
    ]

let private mixBar (name: string, share: int) =
    Html.div [
        prop.key name
        prop.className "mix-row"
        prop.children [
            Html.span name
            Html.div [
                prop.className "mix-track"
                prop.children [
                    Html.div [ prop.className "mix-fill"; prop.style [ style.width (length.percent share) ] ]
                ]
            ]
        ]
    ]

let private mix (graph: JourneyGraph) (model: Model) =
    Html.div [
        prop.className "journey-mix"
        prop.children [
            Html.h2 "Your India so far"
            match JourneyPath.mix graph model.Answers with
            | [] -> Html.p "Keep swiping."
            | rows -> yield! rows |> List.map mixBar
        ]
    ]

let private finishLine (line: string) =
    Html.p [ prop.key line; prop.className "reveal"; prop.text line ]

let private buildNow dispatch =
    Html.button [
        prop.className "journey-build press"
        prop.onClick (fun _ -> dispatch FinishEarly)
        prop.text "Build my trip now"
    ]

/// Once the swipes have settled, the offer to build takes the place of the cheer.
let private finish (settled: bool) (answered: int) dispatch =
    let lines (children: ReactElement list) =
        Html.div [ prop.className "journey-finish"; prop.children children ]

    match settled, JourneyPath.cheer answered with
    | true, _ ->
        lines [
            finishLine JourneyPath.offer
            Html.button [
                prop.className "button journey-offer press"
                prop.onClick (fun _ -> dispatch FinishEarly)
                prop.text "Build my trip"
            ]
        ]
    | false, Some line -> lines [ finishLine line ]
    | false, None -> Html.none

let private swiping graph (model: Model) dispatch =
    let current =
        model.Queue
        |> List.tryHead
        |> Option.bind (fun slug -> graph.Cards |> List.tryFind (fun card -> card.Slug = slug))

    let number = model.Answers.Length + 1
    let settled = JourneyPath.settled graph model.Answers

    match current with
    | Some card ->
        shell $"Card {number} of about {JourneyPath.maxCards}" [
            Html.div [
                prop.key card.Slug
                prop.className (
                    if number = 1 then
                        "journey-stage reveal"
                    else
                        "journey-stage"
                )
                prop.children [
                    mix graph model
                    SwipeCard.SwipeCard(
                        card,
                        not model.Answers.IsEmpty,
                        Decided >> dispatch,
                        (fun () -> dispatch Undone),
                        // The offer below carries the build button once the swipes have settled.
                        (if model.Answers.IsEmpty || settled then
                             Html.none
                         else
                             buildNow dispatch)
                    )
                ]
            ]
            finish settled model.Answers.Length dispatch
        ]
    | None ->
        shell "" [
            Html.main [ States.message "The journey isn't ready yet. Please come back soon." ]
        ]

let private optionButton dispatch (compact: bool) (chosen: bool) (option': Questions.Option') =
    Html.button [
        prop.key option'.Name
        prop.className [
            "choice press"
            if compact then
                "choice-compact"
            if chosen then
                "choice-chosen"
        ]
        prop.custom ("aria-pressed", chosen)
        prop.onClick (fun _ -> dispatch (Chose option'.Choice))
        prop.children [
            Html.strong option'.Name
            if option'.Note <> "" then
                Html.span option'.Note
        ]
    ]

let private question (model: Model) index dispatch =
    let item = Questions.all[index]

    let chosen (option': Questions.Option') =
        index < model.Furthest && isChosen model.Plan option'.Choice

    Html.div [
        prop.key item.Title
        prop.className "journey-question"
        prop.children [
            Html.div [
                prop.className "card-label"
                prop.children [
                    Html.div [
                        prop.className "card-frame question-frame"
                        prop.children [
                            Html.button [
                                prop.className "journey-back press"
                                prop.onClick (fun _ -> dispatch WentBack)
                                prop.children [ Icons.back; Html.span "Back" ]
                            ]
                            Html.h1 item.Title
                            Html.p item.Line
                            Html.div [
                                prop.className (
                                    if item.Compact then
                                        "choices choices-compact"
                                    else
                                        "choices"
                                )
                                prop.children (
                                    item.Options
                                    |> List.map (fun option' ->
                                        optionButton dispatch item.Compact (chosen option') option')
                                )
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private asking model index dispatch =
    let progress =
        if index + 1 = State.questionCount then
            "Last one"
        else
            "Nearly there"

    shell progress [ question model index dispatch ]

let view (model: Model) dispatch =
    match model.Graph, model.Step with
    | Remote.Loading, _ -> shell "" [ Skeletons.swipeCard "Getting your cards ready" ]
    | Remote.Failed error, _ -> shell "" [ Html.main [ States.failed error (fun () -> dispatch LoadGraph) ] ]
    | Remote.Loaded graph, Step.Swiping -> swiping graph model dispatch
    | Remote.Loaded _, Step.Asking index -> asking model index dispatch
    | Remote.Loaded _, Step.Building -> Skeletons.trip "Building your trip" 3
    | Remote.Loaded _, Step.Failed error ->
        shell "" [ Html.main [ States.failed error (fun () -> dispatch BuildTrip) ] ]
