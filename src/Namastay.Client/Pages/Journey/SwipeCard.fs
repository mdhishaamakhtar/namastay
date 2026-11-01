module Namastay.Client.Pages.Journey.SwipeCard

open Fable.Core
open Browser
open Browser.Types
open Feliz
open Namastay.Client.Api
open Namastay.Shared.Contracts

[<ImportMember("./swipe.js")>]
let private attachSwipe (element: HTMLElement) (onDecide: bool -> unit) : (unit -> unit) = jsNative

[<ImportMember("./swipe.js")>]
let private leave (element: HTMLElement) (liked: bool) (onDecide: bool -> unit) : unit = jsNative

let private frameColour (card: JourneyCard) =
    card.Colours |> Option.map _.Deep |> Option.defaultValue "#2B2108"

let private mark (card: JourneyCard) =
    match card.MarkUrl with
    | Some url ->
        Html.div [
            prop.className "card-mark"
            prop.children [ Html.img [ prop.src (Client.fromApi url); prop.alt "" ] ]
        ]
    | None -> Html.none

let private photo (card: JourneyCard) =
    match card.ImageUrl with
    | Some url ->
        Html.img [
            prop.className "card-photo"
            prop.src (Client.fromApi url)
            prop.alt (card.ImageAlt |> Option.defaultValue "")
            prop.draggable false
        ]
    | None -> Html.div [ prop.className "card-photo" ]

let private label (card: JourneyCard) =
    Html.div [
        prop.className "card-frame"
        prop.style [ style.borderColor (frameColour card) ]
        prop.children [
            photo card
            Html.div [
                prop.className "card-text"
                prop.children [ Html.div [ Html.h1 card.Title; Html.p card.Line ]; mark card ]
            ]
            match card.ImageCredit with
            | Some credit -> Html.p [ prop.className "card-credit"; prop.text credit ]
            | None -> Html.none
        ]
    ]

/// `onDecide` runs once, after the card has left the screen.
[<ReactComponent>]
let SwipeCard (card: JourneyCard, onDecide: bool -> unit) =
    let element = React.useElementRef ()
    let leaving = React.useRef false

    let decide liked =
        match element.current with
        | Some current when not leaving.current ->
            leaving.current <- true
            leave current liked onDecide
        | _ -> ()

    let setup () : (unit -> unit) =
        let detach =
            match element.current with
            | Some current ->
                attachSwipe current (fun liked ->
                    leaving.current <- true
                    onDecide liked)
            | None -> ignore

        let onKey (event: Event) =
            match (event :?> KeyboardEvent).key with
            | "ArrowRight" -> decide true
            | "ArrowLeft" -> decide false
            | _ -> ()

        window.addEventListener ("keydown", onKey)

        fun () ->
            detach ()
            window.removeEventListener ("keydown", onKey)

    React.useEffect (setup, [||])

    React.Fragment [
        Html.div [ prop.ref element; prop.className "card-label"; prop.children [ label card ] ]
        Html.div [
            prop.className "journey-actions"
            prop.children [
                Html.button [
                    prop.className "button action-love press"
                    prop.text "Love it"
                    prop.onClick (fun _ -> decide true)
                ]
                Html.button [
                    prop.className "button action-pass press"
                    prop.text "Not for me"
                    prop.onClick (fun _ -> decide false)
                ]
                Html.p [
                    prop.className "journey-hint"
                    prop.text "Or drag the card, or press the left and right arrow keys."
                ]
            ]
        ]
    ]
