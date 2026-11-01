module Namastay.Client.Pages.Browse.Food

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Browse.Parts
open Namastay.Client.Pages.Browse.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private dishLine (dish: FoodDish) =
    Html.li [
        prop.key dish.Slug
        prop.children [
            Html.a [
                prop.href (Route.url [ "slug", dish.Slug ] [] Routes.dishPage)
                prop.text dish.Name
            ]
        ]
    ]

let private foodRegion (searching: bool) (region: FoodRegion) =
    let lines =
        if searching then
            region.Dishes
        else
            region.Dishes |> List.truncate shownInCard

    let href = Route.url [ "slug", region.Slug ] [] Routes.foodRegionPage

    Html.section [
        prop.key region.Slug
        prop.className "region-card"
        prop.children [
            Html.a [
                prop.className "region-top"
                prop.style [ style.backgroundColor region.Colour ]
                prop.href href
                prop.children [
                    Html.img [ prop.src $"/regions/food-{region.Slug}.svg"; prop.alt "" ]
                    Html.span [
                        prop.className "region-name"
                        prop.children [ Html.strong (heading region.Name) ]
                    ]
                ]
            ]
            Html.ul [ prop.children (lines |> List.map dishLine) ]
            if not searching then
                seeAll "All dishes" href
        ]
    ]

let private foodCard (card: LandingCard) =
    Html.a [
        prop.key card.Title
        prop.className "label-link"
        prop.href (Route.url [ "slug", PlaceName.slug card.Title ] [] Routes.dishPage)
        prop.children [ Label.view card.Colour (Some card.Photo) card.Title [ heading card.Place ] ]
    ]

/// One card per region would bury the regions, so only the first few lead.
let private leadingFood = 3

let private matchingDishes (query: string) (region: FoodRegion) = {
    region with
        Dishes = region.Dishes |> List.filter (fun dish -> matches query dish.Name)
}

let private foodResults (model: Model) (page: FoodPage) =
    let shown =
        page.Regions
        |> List.map (matchingDishes model.Query)
        |> List.filter (fun region -> not region.Dishes.IsEmpty)

    if shown.IsEmpty then
        Html.p [ prop.className "no-results"; prop.text "We don't have that dish yet." ]
    else
        Html.div [
            prop.className "region-grid reveal"
            prop.children (shown |> List.map (foodRegion true))
        ]

let private foodBrowsing (page: FoodPage) = [
    Html.div [
        prop.className "label-grid home-food-grid reveal"
        prop.children (page.Cards |> List.truncate leadingFood |> List.map foodCard)
    ]
    Html.h2 "By region"
    Html.div [
        prop.className "region-grid reveal"
        prop.children (page.Regions |> List.map (foodRegion false))
    ]
]

let page (model: Model) dispatch (page: FoodPage) =
    Html.div [
        prop.className "browse"
        prop.children [
            Seo.View {
                Title = "What to eat in India, region by region | Namastay"
                Description =
                    "The dishes worth finding across India, what they are, and where people go for them. A food guide for first-time visitors."
                Path = Routes.foodPage.Template
                Image = None
                Trail = [ "Home", "/"; "Food", Routes.foodPage.Template ]
            }
            head
                Menu.Section.Food
                None
                "Eat your way across India"
                "What to order, and where people go for it."
                anywhere
                Html.none
            Html.main [
                prop.children [
                    Html.section [
                        prop.className "browse-body"
                        prop.children [
                            searchBox "Find a dish" "Find a dish: thukpa, dosa, kachori" model dispatch
                            if model.Query.Trim() = "" then
                                yield! foodBrowsing page
                            else
                                foodResults model page
                        ]
                    ]
                ]
            ]
        ]
    ]
