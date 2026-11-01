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

let private finish (answered: int) dispatch =
    Html.div [
        prop.className "journey-finish"
        prop.children [
            match JourneyPath.cheer answered with
            | Some line -> Html.p [ prop.key line; prop.className "reveal"; prop.text line ]
            | None -> Html.none
            if answered > 0 then
                Html.button [
                    prop.className "journey-build press"
                    prop.onClick (fun _ -> dispatch FinishEarly)
                    prop.text "Build my trip now"
                ]
        ]
    ]

let private swiping graph (model: Model) dispatch =
    let current =
        model.Queue
        |> List.tryHead
        |> Option.bind (fun slug -> graph.Cards |> List.tryFind (fun card -> card.Slug = slug))

    let number = model.Answers.Length + 1

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
                prop.children [ mix graph model; SwipeCard.SwipeCard(card, Decided >> dispatch) ]
            ]
            finish model.Answers.Length dispatch
        ]
    | None ->
        shell "" [
            Html.main [ States.message "The journey isn't ready yet. Please come back soon." ]
        ]

let private optionButton dispatch (compact: bool) (option': Questions.Option') =
    Html.button [
        prop.key option'.Name
        prop.className (
            if compact then
                "choice choice-compact press"
            else
                "choice press"
        )
        prop.onClick (fun _ -> dispatch (Chose option'.Choice))
        prop.children [
            Html.strong option'.Name
            if option'.Note <> "" then
                Html.span option'.Note
        ]
    ]

let private question index dispatch =
    let item = Questions.all[index]

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
                            Html.h1 item.Title
                            Html.p item.Line
                            Html.div [
                                prop.className (
                                    if item.Compact then
                                        "choices choices-compact"
                                    else
                                        "choices"
                                )
                                prop.children (item.Options |> List.map (optionButton dispatch item.Compact))
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private asking index dispatch =
    let progress =
        if index + 1 = State.questionCount then
            "Last one"
        else
            "Nearly there"

    shell progress [ question index dispatch ]

let view (model: Model) dispatch =
    match model.Graph, model.Step with
    | Remote.Loading, _ -> shell "" [ Skeletons.swipeCard "Getting your cards ready" ]
    | Remote.Failed error, _ -> shell "" [ Html.main [ States.failed error (fun () -> dispatch LoadGraph) ] ]
    | Remote.Loaded graph, Step.Swiping -> swiping graph model dispatch
    | Remote.Loaded _, Step.Asking index -> asking index dispatch
    | Remote.Loaded _, Step.Building -> Skeletons.trip "Building your trip" 3
    | Remote.Loaded _, Step.Failed error ->
        shell "" [ Html.main [ States.failed error (fun () -> dispatch BuildTrip) ] ]
