module Namastay.Client.Pages.Home.View

open Feliz
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
            prop.href (Route.url [ "slug", place.Slug ] [] Routes.guidePage)
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

let private results (model: Model) dispatch =
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

let private searchBox (model: Model) dispatch =
    Html.section [
        prop.className "home-search"
        prop.children [
            Html.label [
                prop.children [
                    Html.span [ prop.className "home-heading"; prop.text "Or look up a place" ]
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

let private moodTile dispatch (mood: Mood) =
    Html.button [
        prop.key mood.Name
        prop.className "mood press"
        prop.style [ style.backgroundColor mood.Colour ]
        prop.onClick (fun _ -> dispatch (QueryChanged mood.Search))
        prop.children [ Html.strong mood.Name; Html.span mood.Examples ]
    ]

let private moodSection dispatch =
    Html.section [
        prop.className "home-moods"
        prop.children [
            Html.h2 [ prop.className "home-heading"; prop.text "Or start with a feeling" ]
            Html.div [
                prop.className "moods"
                prop.children (moods |> List.map (moodTile dispatch))
            ]
        ]
    ]

let private hero (navigate: string -> unit) =
    Html.section [
        prop.className "home-hero"
        prop.children [
            Html.div [ prop.className "home-brand"; prop.text "Namastay" ]
            Html.div [
                prop.className "home-pitch"
                prop.children [
                    Html.h1 "India is a lot. Find yours."
                    Html.p "A guide to India for people who have never been, or have only seen one part of it."
                    Html.button [
                        prop.className "button home-plan press"
                        prop.text "Plan my trip"
                        prop.onClick (fun _ -> navigate Routes.journey.Template)
                    ]
                ]
            ]
            Html.p [ prop.className "home-free"; prop.text "Free, and no sign-up needed." ]
        ]
    ]

let view (model: Model) dispatch (navigate: string -> unit) =
    Html.div [
        prop.className "home"
        prop.children [
            hero navigate
            Html.main [
                prop.className "home-find"
                prop.children [ searchBox model dispatch; moodSection dispatch ]
            ]
        ]
    ]
