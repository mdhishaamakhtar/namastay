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
    Note: string
    Scope: string option
    Slug: string
    Name: string
}

[<Emit("encodeURIComponent($0)")>]
let private encode (text: string) : string = jsNative

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
            Html.span plan.Note
        ]
    ]

let private anywhere = {
    Label = "Plan a trip"
    Note = "Cards from all of India."
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

let private regionCard (region: DirectoryRegion) =
    Html.section [
        prop.key region.Slug
        prop.className "region-card"
        prop.children [
            Html.a [
                prop.className "region-top"
                prop.style [ style.backgroundColor region.Colour ]
                prop.href (Route.url [ "slug", region.Slug ] [] Routes.regionPage)
                prop.children [
                    Html.img [ prop.src $"/regions/{region.Slug}.svg"; prop.alt "" ]
                    Html.span [
                        prop.className "region-name"
                        prop.children [ Html.strong region.Name; Html.span (string region.Places.Length) ]
                    ]
                ]
            ]
            Html.ul [ prop.children (region.Places |> List.map placeLine) ]
        ]
    ]

let private matching (query: string) (region: DirectoryRegion) =
    let wanted = query.Trim().ToLowerInvariant()

    {
        region with
            Places =
                region.Places
                |> List.filter (fun place -> place.Name.ToLowerInvariant().Contains wanted)
    }

let private placesBody (model: Model) dispatch (regions: DirectoryRegion list) =
    let shown =
        regions
        |> List.map (matching model.Query)
        |> List.filter (fun region -> not region.Places.IsEmpty)

    Html.section [
        prop.className "browse-body"
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
            if shown.IsEmpty then
                Html.p [
                    prop.className "no-results"
                    prop.text "We don't have a guide for that yet. Try a nearby place or a region."
                ]
            else
                Html.div [
                    prop.className "region-grid reveal"
                    prop.children (shown |> List.map regionCard)
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
                prop.className "food-line"
                prop.href (Route.url [ "slug", dish.Slug ] [] Routes.dishPage)
                prop.children [ Html.strong dish.Name; Html.span dish.Place ]
            ]
        ]
    ]

let private foodRegion (region: FoodRegion) =
    Html.section [
        prop.key region.Slug
        prop.className "region-card"
        prop.children [
            Html.a [
                prop.className "region-top region-name"
                prop.style [ style.backgroundColor region.Colour ]
                prop.href (Route.url [ "slug", region.Slug ] [] Routes.foodRegionPage)
                prop.children [ Html.strong region.Name ]
            ]
            Html.ul [ prop.children (region.Dishes |> List.map dishLine) ]
        ]
    ]

let private foodCard (card: LandingCard) =
    Html.a [
        prop.key card.Title
        prop.className "label-link"
        prop.href (Route.url [ "slug", card.PlaceSlug ] [] Routes.guidePage)
        prop.children [ Label.view card.Colour (Some card.Photo) card.Title [ card.Place ] ]
    ]

let private foodPage (page: FoodPage) =
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
                            Html.div [
                                prop.className "label-grid reveal"
                                prop.children (page.Cards |> List.map foodCard)
                            ]
                        ]
                    ]
                    Html.section [
                        prop.className "browse-body"
                        prop.children [
                            Html.h2 "By region"
                            Html.div [
                                prop.className "region-grid reveal"
                                prop.children (page.Regions |> List.map foodRegion)
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private listedPage (target: Browse) (page: BrowsePage) =
    let plan label note scope = {
        Label = label
        Note = note
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
            (plan $"Plan a food trip in {page.Name}" $"Food cards from {page.Name} only. Your trip stays here." "food")
            "/feelings/food.svg"
            "Dishes here"
            dishUrl
    | Browse.Feeling slug ->
        page'
            Menu.Section.Places
            allPlaces
            page.Name
            $"{page.Items.Length} places that suit it."
            (plan $"Plan a {page.Name.ToLowerInvariant()} trip" "Cards about this, from anywhere in India." "feeling")
            $"/feelings/{slug}.svg"
            "Places for this"
            guideUrl
    | _ ->
        page'
            Menu.Section.Places
            allPlaces
            page.Name
            $"{page.Items.Length} places."
            (plan $"Plan a trip in {page.Name}" $"Cards from {page.Name} only. Your trip stays here." "region")
            $"/regions/{page.Slug}.svg"
            "Places here"
            guideUrl

let private dishPage (page: DishPage) =
    let plan = {
        Label = $"Plan a trip around {page.Name.ToLowerInvariant()}"
        Note = "Takes you to the places below."
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
    | Remote.Loaded(Content.Food page) -> foodPage page
    | Remote.Loaded(Content.Listed page) -> listedPage model.Target page
    | Remote.Loaded(Content.Dish page) -> dishPage page
