module Namastay.Client.Pages.Browse.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Browse.Parts
open Namastay.Client.Pages.Browse
open Namastay.Client.Pages.Browse.State
open Namastay.Client.Routing.Page
open Namastay.Shared
open Namastay.Shared.Contracts

let private listedPage (target: Browse) (page: BrowsePage) =
    let plan label scope = {
        Label = label
        Scope = Some scope
        Slug = page.Slug
        Name = page.Name
    }

    let heading (text: string) =
        text.Substring(0, 1).ToUpperInvariant() + text.Substring 1

    let page' section crumb (title: string) line chosen art listHeading href =
        Html.div [
            prop.className "browse"
            prop.children [
                head section (Some crumb) (heading title) line chosen (drawing page.Colour art)
                Html.main [ prop.children [ listed listHeading href page.Items ] ]
            ]
        ]

    let allPlaces = "All places", Routes.explorePage.Template

    match target with
    | Browse.FoodRegion _ ->
        page'
            Menu.Section.Food
            ("All food", Routes.foodPage.Template)
            $"Food of {page.Name}"
            $"{page.Items.Length} dishes worth finding."
            (plan $"Plan a food trip in {page.Name}" "food")
            "/feelings/food.svg"
            "Dishes here"
            dishUrl
    | Browse.Feeling slug ->
        page'
            Menu.Section.Places
            allPlaces
            page.Name
            $"{page.Items.Length} places that suit it."
            (plan $"Plan a {page.Name.ToLowerInvariant()} trip" "feeling")
            $"/feelings/{slug}.svg"
            "Places for this"
            guideUrl
    | _ ->
        page'
            Menu.Section.Places
            allPlaces
            page.Name
            $"{page.Items.Length} places."
            (plan $"Plan a trip in {page.Name}" "region")
            $"/regions/{page.Slug}.svg"
            "Places here"
            guideUrl

let private dishPage (page: DishPage) =
    let plan = {
        Label = $"Plan a trip around {page.Name.ToLowerInvariant()}"
        Scope = Some "dish"
        Slug = page.Slug
        Name = page.Name
    }

    let art =
        match page.Photo with
        | Some _ ->
            Html.div [
                prop.className "browse-photo"
                prop.children [ Label.view "#A8201A" page.Photo page.Name [] ]
            ]
        | None -> drawing "#A8201A" "/feelings/food.svg"

    Html.div [
        prop.className "browse"
        prop.children [
            head Menu.Section.Food (Some("All food", Routes.foodPage.Template)) page.Name page.Description plan art
            Html.main [ prop.children [ listed "Where to have it" guideUrl page.Places ] ]
        ]
    ]

let view (model: Model) dispatch =
    match model.Content with
    | Remote.Loading -> Skeletons.browse "Finding this page"
    | Remote.Failed error ->
        Html.div [
            prop.className "browse"
            prop.children [
                Html.header [
                    prop.className "browse-head"
                    prop.children [ Menu.view Menu.Section.Places ]
                ]
                Html.main [ prop.children [ States.failed error (fun () -> dispatch Load) ] ]
            ]
        ]
    | Remote.Loaded(Content.Places regions) -> Places.page model dispatch regions
    | Remote.Loaded(Content.Food page) -> Food.page model dispatch page
    | Remote.Loaded(Content.Listed page) -> listedPage model.Target page
    | Remote.Loaded(Content.Dish page) -> dishPage page
