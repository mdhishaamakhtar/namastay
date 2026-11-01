module Namastay.Client.Pages.Home.Search

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.Home.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private colourBar (colour: string) =
    Html.span [ prop.className "result-colour"; prop.style [ style.backgroundColor colour ] ]

let private resultText (place: PlaceSummary) (note: string) =
    Html.span [
        prop.className "result-text"
        prop.children [ Html.strong place.Name; Html.span note ]
    ]

let private result (place: PlaceSummary) =
    if place.Published then
        // A guide is a server page, so this is a normal link.
        Html.a [
            prop.key place.Slug
            prop.className "result press"
            prop.href (Client.fromApi (Route.url [ "slug", place.Slug ] [] Routes.guidePage))
            prop.children [ colourBar place.FamilyDeep; resultText place place.Region ]
        ]
    else
        Html.div [
            prop.key place.Slug
            prop.className "result"
            prop.children [
                colourBar place.FamilyDeep
                resultText place $"{place.Region}, guide coming soon"
            ]
        ]

let results (model: Model) dispatch =
    match model.Results with
    | None -> Html.none
    | Some Remote.Loading -> Html.none
    | Some(Remote.Failed error) -> States.failed error (fun () -> dispatch Retry)
    | Some(Remote.Loaded []) ->
        Html.p [
            prop.className "no-results"
            prop.text "We don't have a guide for that yet. Try a nearby place or a region."
        ]
    | Some(Remote.Loaded places) -> Html.div [ prop.className "results"; prop.children (places |> List.map result) ]

let searchBox (model: Model) dispatch =
    Html.div [
        prop.className "home-search"
        prop.children [
            Html.label [
                prop.children [
                    Html.span "Or look up a place"
                    Html.input [
                        prop.className "search-input"
                        prop.type' "search"
                        prop.placeholder "Try Goa, Varanasi or Ladakh"
                        prop.autoComplete "off"
                        prop.value model.Query
                        prop.onChange (QueryChanged >> dispatch)
                    ]
                ]
            ]
            Html.div [
                prop.custom ("aria-live", "polite")
                prop.children [ results model dispatch ]
            ]
        ]
    ]

/// The search box alone, for pages where a heading already says what it's for.
let box (model: Model) dispatch =
    Html.div [
        prop.className "search-box"
        prop.children [
            Html.input [
                prop.className "search-input"
                prop.type' "search"
                prop.ariaLabel "Find a place"
                prop.placeholder "Find a place: Goa, Varanasi, Ladakh"
                prop.autoComplete "off"
                prop.value model.Query
                prop.onChange (QueryChanged >> dispatch)
            ]
            Html.div [
                prop.className "search-found"
                prop.custom ("aria-live", "polite")
                prop.children [ results model dispatch ]
            ]
        ]
    ]
