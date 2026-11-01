module Namastay.Client.Pages.Browse.Places

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.Browse.Parts
open Namastay.Client.Pages.Browse.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private placeLine (place: PlaceSummary) =
    Html.li [
        prop.key place.Slug
        prop.children [
            Html.a [
                prop.href (Client.fromApi (Route.url [ "slug", place.Slug ] [] Routes.guidePage))
                prop.children [
                    Html.span [
                        prop.className "listing-colour"
                        prop.style [ style.backgroundColor place.FamilyDeep ]
                    ]
                    Html.span place.Name
                ]
            ]
        ]
    ]

let private regionCard (searching: bool) (region: DirectoryRegion) =
    let href = Route.url [ "slug", region.Slug ] [] Routes.regionPage

    let lines =
        if searching then
            region.Places
        else
            region.Places |> List.truncate shownInCard

    Html.section [
        prop.key region.Slug
        prop.className "region-card"
        prop.children [
            Html.a [
                prop.className "region-top"
                prop.style [ style.backgroundColor region.Colour ]
                prop.href href
                prop.children [
                    Html.img [ prop.src $"/regions/{region.Slug}.svg"; prop.alt "" ]
                    Html.span [
                        prop.className "region-name"
                        prop.children [ Html.strong (heading region.Name); Html.span (string region.Places.Length) ]
                    ]
                ]
            ]
            Html.ul [ prop.children (lines |> List.map placeLine) ]
            if not searching then
                seeAll ("All " + counted region.Places.Length "place") href
        ]
    ]

let private matching (query: string) (region: DirectoryRegion) = {
    region with
        Places = region.Places |> List.filter (fun place -> matches query place.Name)
}

let private placesBody (model: Model) dispatch (regions: DirectoryRegion list) =
    let shown =
        regions
        |> List.map (matching model.Query)
        |> List.filter (fun region -> not region.Places.IsEmpty)

    Html.section [
        prop.className "browse-body"
        prop.children [
            searchBox "Find a place" "Find a place: Goa, Varanasi, Ladakh" model dispatch
            if shown.IsEmpty then
                Html.p [
                    prop.className "no-results"
                    prop.text "We don't have a guide for that yet. Try a nearby place or a region."
                ]
            else
                Html.div [
                    prop.className "region-grid reveal"
                    prop.children (shown |> List.map (regionCard (model.Query.Trim() <> "")))
                ]
        ]
    ]

let page (model: Model) dispatch (regions: DirectoryRegion list) =
    let count = regions |> List.sumBy _.Places.Length

    Html.div [
        prop.className "browse"
        prop.children [
            head
                Menu.Section.Places
                None
                $"{count} places across India"
                "Pick a region, or search for a name."
                anywhere
                Html.none
            Html.main [ prop.children [ placesBody model dispatch regions ] ]
        ]
    ]
