module Namastay.Client.Pages.Browse.View

open Feliz
open Namastay.Client.Api
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

    let places = page.Items |> List.truncate 3 |> List.map _.Name |> String.concat ", "

    let page' (seo: Seo.Page) section crumb (title: string) line chosen art listHeading href =
        Html.div [
            prop.className "browse"
            prop.children [
                Seo.View seo
                head section (Some crumb) (heading title) line chosen (drawing page.Colour art)
                Html.main [ prop.children [ listed listHeading href page.Items ] ]
            ]
        ]

    let allPlaces = "All places", Routes.explorePage.Template

    let trail (parent: string * string) (path: string) (name: string) = [ "Home", "/"; parent; heading name, path ]

    let food = "Food", Routes.foodPage.Template

    match target with
    | Browse.FoodRegion _ ->
        let path = Route.url [ "slug", page.Slug ] [] Routes.foodRegionPage

        page'
            {
                Title = $"Food of {page.Name}: what to eat and where | Namastay"
                Description = $"The dishes worth finding in {page.Name}, including {places}, and where to have them."
                Path = path
                Image = None
                Trail = trail food path page.Name
            }
            Menu.Section.Food
            ("All food", Routes.foodPage.Template)
            $"Food of {page.Name}"
            $"{page.Items.Length} dishes worth finding."
            (plan $"Plan a food trip in {page.Name}" "food")
            "/feelings/food.svg"
            "Dishes here"
            dishUrl
    | Browse.Feeling slug ->
        let path = Route.url [ "slug", slug ] [] Routes.feelingPage

        page'
            {
                Title = $"{heading page.Name} in India: the best places to go | Namastay"
                Description =
                    $"Where to go in India for {page.Name.ToLowerInvariant()}, including {places}. Honest guides for first-time visitors."
                Path = path
                Image = None
                Trail = trail ("Places", Routes.explorePage.Template) path page.Name
            }
            Menu.Section.Places
            allPlaces
            page.Name
            $"{page.Items.Length} places that suit it."
            (plan $"Plan a {page.Name.ToLowerInvariant()} trip" "feeling")
            $"/feelings/{slug}.svg"
            "Places for this"
            guideUrl
    | _ ->
        let path = Route.url [ "slug", page.Slug ] [] Routes.regionPage

        page'
            {
                Title = $"{heading page.Name} travel guide: where to go | Namastay"
                Description =
                    $"The best places to visit in {page.Name}, including {places}. Honest guides for first-time visitors to India."
                Path = path
                Image = None
                Trail = trail ("Places", Routes.explorePage.Template) path page.Name
            }
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
                prop.children [ Label.withBadge (DietMark.badge page.Kind) "#A8201A" page.Photo page.Name [] ]
            ]
        | None -> drawing "#A8201A" "/feelings/food.svg"

    Html.div [
        prop.className "browse"
        prop.children [
            Seo.View {
                Title = $"{page.Name}: what it is and where to eat it in India | Namastay"
                Description = page.Description
                Path = Route.url [ "slug", page.Slug ] [] Routes.dishPage
                Image = page.Photo |> Option.map _.Url
                Trail = [
                    "Home", "/"
                    "Food", Routes.foodPage.Template
                    page.Name, Route.url [ "slug", page.Slug ] [] Routes.dishPage
                ]
            }
            head Menu.Section.Food (Some("All food", Routes.foodPage.Template)) page.Name page.Description plan art
            Html.main [ prop.children [ listed "Where to have it" guideUrl page.Places ] ]
        ]
    ]

let view (model: Model) dispatch =
    match model.Content with
    | Remote.Loading ->
        match model.Target with
        | Browse.Places -> Skeletons.places "Finding places"
        | Browse.Food -> Skeletons.food "Finding food"
        | Browse.FoodRegion _
        | Browse.Dish _ -> Skeletons.listed "Finding this page" Menu.Section.Food
        | _ -> Skeletons.listed "Finding this page" Menu.Section.Places
    | Remote.Failed error when Client.isNotFound error -> NotFound.view
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
