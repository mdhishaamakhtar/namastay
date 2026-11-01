module Namastay.Client.Pages.Browse.View

open Feliz
open Fable.Core
open Namastay.Client.Components
open Namastay.Client.Pages.Browse.State
open Namastay.Client.Routing.Page
open Namastay.Shared
open Namastay.Shared.Contracts

type private Plan = {
    Label: string
    Scope: string option
    Slug: string
    Name: string
}

[<Emit("encodeURIComponent($0)")>]
let private encode (text: string) : string = jsNative

/// Region names read mid-sentence, like "the Himalaya", so a heading capitalises them.
let private heading (text: string) =
    text.Substring(0, 1).ToUpperInvariant() + text.Substring 1

/// Cards stay the same height, and the link leads to the rest.
let private shownInCard = 5

let private planUrl (plan: Plan) =
    match plan.Scope with
    | Some scope -> $"{Routes.journey.Template}?scope={scope}&of={plan.Slug}&label={encode plan.Name}"
    | None -> Routes.journey.Template

let private planButton (plan: Plan) =
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

let private anywhere = {
    Label = "Plan a trip"
    Scope = None
    Slug = ""
    Name = ""
}

let private head section (crumb: (string * string) option) (title: string) (line: string) plan (art: ReactElement) =
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
                            planButton plan
                        ]
                    ]
                    art
                ]
            ]
        ]
    ]

let private drawing (colour: string) (url: string) =
    Html.div [
        prop.className "browse-art"
        prop.style [ style.backgroundColor colour ]
        prop.children [ Html.img [ prop.src url; prop.alt "" ] ]
    ]

let private listing (href: Listing -> string) (item: Listing) =
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
                prop.children [ Html.strong item.Name; Html.span item.Line ]
            ]
        ]
    ]

let private guideUrl (item: Listing) =
    Route.url [ "slug", item.Slug ] [] Routes.guidePage

let private dishUrl (item: Listing) =
    Route.url [ "slug", item.Slug ] [] Routes.dishPage

let private listed (heading: string) href (items: Listing list) =
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

let private placeLine (place: PlaceSummary) =
    Html.li [
        prop.key place.Slug
        prop.children [
            Html.a [
                prop.href (Route.url [ "slug", place.Slug ] [] Routes.guidePage)
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

let private seeAll (label: string) (href: string) =
    Html.a [ prop.className "region-all"; prop.href href; prop.text label ]

let private counted (count: int) (noun: string) =
    if count = 1 then $"1 {noun}" else $"{count} {noun}s"

/// Searching shows every match. Browsing shows a few and links to the rest.
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

let private matches (query: string) (name: string) =
    name.ToLowerInvariant().Contains(query.Trim().ToLowerInvariant())

let private matching (query: string) (region: DirectoryRegion) = {
    region with
        Places = region.Places |> List.filter (fun place -> matches query place.Name)
}

let private searchBox (label: string) (hint: string) (model: Model) dispatch =
    Html.input [
        prop.className "search-input"
        prop.type' "search"
        prop.ariaLabel label
        prop.placeholder hint
        prop.autoComplete "off"
        prop.value model.Query
        prop.onChange (QueryChanged >> dispatch)
    ]

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

let private placesPage (model: Model) dispatch (regions: DirectoryRegion list) =
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

let private foodPage (model: Model) dispatch (page: FoodPage) =
    Html.div [
        prop.className "browse"
        prop.children [
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
    | Remote.Loaded(Content.Places regions) -> placesPage model dispatch regions
    | Remote.Loaded(Content.Food page) -> foodPage model dispatch page
    | Remote.Loaded(Content.Listed page) -> listedPage model.Target page
    | Remote.Loaded(Content.Dish page) -> dishPage page
