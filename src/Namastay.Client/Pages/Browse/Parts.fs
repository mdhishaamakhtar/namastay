/// What the Places, Food and listing pages share.
module Namastay.Client.Pages.Browse.Parts

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.Browse.State
open Namastay.Client.Routing.Page
open Namastay.Shared
open Namastay.Shared.Contracts

type Plan = {
    Label: string
    /// What the trip is limited to. None plans from anywhere.
    Scope: TripScope option
    /// How the journey names that limit.
    Name: string
}

/// Region names read mid-sentence, like "the Himalaya", so a heading capitalises them.
let heading (text: string) =
    text.Substring(0, 1).ToUpperInvariant() + text.Substring 1

/// Cards stay the same height, and the link leads to the rest.
let shownInCard = 5

let planUrl (plan: Plan) =
    match plan.Scope with
    | Some scope -> Route.url [] (TripScopes.toQuery scope @ [ "label", plan.Name ]) Routes.journey
    | None -> Routes.journey.Template

let planButton (plan: Plan) =
    Html.div [
        prop.className "browse-plan"
        prop.children [
            Html.a [
                prop.className "button press"
                prop.href (planUrl plan)
                prop.text plan.Label
            ]
        ]
    ]

let anywhere = {
    Label = "Plan a trip"
    Scope = None
    Name = ""
}

/// `plan` is the plan button, with anything a page offers beside it.
let head
    section
    (crumb: (string * string) option)
    (title: string)
    (line: string)
    (plan: ReactElement)
    (art: ReactElement)
    =
    Html.header [
        prop.className "browse-head"
        prop.children [
            Menu.view section
            Html.div [
                prop.className "browse-hero"
                prop.children [
                    Html.div [
                        prop.className "browse-title"
                        prop.children [
                            match crumb with
                            | Some(label, href) ->
                                Html.a [ prop.className "browse-crumb"; prop.href href; prop.text label ]
                            | None -> Html.none
                            Html.h1 title
                            Html.p line
                            plan
                        ]
                    ]
                    art
                ]
            ]
        ]
    ]

let drawing (colour: string) (url: string) =
    Html.div [
        prop.className "browse-art"
        prop.style [ style.backgroundColor colour ]
        prop.children [ Html.img [ prop.src url; prop.alt "" ] ]
    ]

let listing (href: Listing -> string) (item: Listing) =
    Html.a [
        prop.key item.Slug
        prop.className "listing press"
        prop.href (href item)
        prop.children [
            Html.span [
                prop.className "listing-colour"
                prop.style [ style.backgroundColor item.Colour ]
            ]
            Html.span [
                prop.className "listing-text"
                prop.children [
                    Html.span [
                        prop.className "listing-head"
                        prop.children [
                            Html.strong item.Name
                            match item.Kind with
                            | Some kind -> DietMark.label kind
                            | None -> Html.none
                        ]
                    ]
                    Html.span item.Line
                ]
            ]
        ]
    ]

let guideUrl (item: Listing) =
    Route.url [ "slug", item.Slug ] [] Routes.guidePage

let dishUrl (item: Listing) =
    Route.url [ "slug", item.Slug ] [] Routes.dishPage

let listed (heading: string) href (items: Listing list) =
    Html.section [
        prop.className "browse-body"
        prop.children [
            Html.h2 heading
            Html.div [
                prop.className "listings reveal"
                prop.children (items |> List.map (listing href))
            ]
        ]
    ]

let seeAll (label: string) (href: string) =
    Html.a [ prop.className "region-all"; prop.href href; prop.text label ]

let counted (count: int) (noun: string) =
    if count = 1 then $"1 {noun}" else $"{count} {noun}s"

/// Searching shows every match. Browsing shows a few and links to the rest.
let matches (query: string) (name: string) =
    name.ToLowerInvariant().Contains(query.Trim().ToLowerInvariant())

let searchBox (label: string) (hint: string) (model: Model) dispatch =
    Html.input [
        prop.className "search-input"
        prop.type' "search"
        prop.ariaLabel label
        prop.placeholder hint
        prop.autoComplete "off"
        prop.value model.Query
        prop.onChange (QueryChanged >> dispatch)
    ]
