module Namastay.Domain.Catalog.Validation

open System

type ValidationError = {
    Table: string
    Slug: string
    Message: string
}

let private error table slug message = {
    Table = table
    Slug = slug
    Message = message
}

let private checkReference table slug (known: Set<string>) (reference: string) =
    if known.Contains reference then
        []
    else
        [ error table slug $"unknown reference '{reference}'" ]

let private checkEvidence table slug (known: Set<int>) (evidence: Evidence) = [
    if evidence.Status <> FactStatus.Editorial && evidence.SourceIds.IsEmpty then
        error table slug "fact has no source"

    for id in evidence.SourceIds do
        if not (known.Contains id) then
            error table slug $"unknown source {id}"
]

let private checkDates table slug (validFrom: DateOnly option) (validTo: DateOnly option) =
    match validFrom, validTo with
    | Some starts, Some ends when ends <= starts -> [ error table slug "valid_to must follow valid_from" ]
    | _ -> []

let private placeEvidence (place: Place) = [
    place.Evidence
    yield! place.Months |> List.map _.Evidence
    yield! place.Experiences |> List.map _.Evidence
    yield! place.DayPlans |> List.map _.Evidence
    yield! place.StayAreas |> List.map _.Evidence
]

let private checkPublished (place: Place) =
    let hasUnverifiedCopy =
        place.Experiences
        |> List.map _.Evidence
        |> List.append (place.DayPlans |> List.map _.Evidence)
        |> List.append (place.StayAreas |> List.map _.Evidence)
        |> List.append [ place.Evidence ]
        |> List.exists (fun evidence -> evidence.Status = FactStatus.Unverified)

    [
        if not (place.Months |> List.exists (fun month -> month.Rating = Rating.Best)) then
            error "places" place.Slug "published place needs a best month"
        if place.StayAreas.IsEmpty then
            error "places" place.Slug "published place needs a stay area"
        if place.DayPlans.IsEmpty then
            error "places" place.Slug "published place needs a day plan"
        if hasUnverifiedCopy then
            error "places" place.Slug "published place has unverified visitor copy"
    ]

let private checkPlace clusters families interests sources (place: Place) = [
    yield! checkReference "places" place.Slug clusters place.ClusterSlug
    yield! checkReference "places" place.Slug families place.FamilySlug

    for interest in place.Interests do
        yield! checkReference "place_interests" place.Slug interests interest.InterestSlug

    for month in place.Months do
        if month.Month < 1 || month.Month > 12 then
            error "place_months" place.Slug $"month {month.Month} is outside 1-12"

    for evidence in placeEvidence place do
        yield! checkEvidence "places" place.Slug sources evidence

    if place.Published then
        yield! checkPublished place
]

let private checkLink places sources (link: TransportLink) = [
    yield! checkReference "transport_links" link.Slug places link.FromSlug
    yield! checkReference "transport_links" link.Slug places link.ToSlug
    if link.MinutesMin <= 0 || link.MinutesMax < link.MinutesMin then
        error "transport_links" link.Slug "route needs positive minutes"
    yield! checkDates "transport_links" link.Slug link.ValidFrom link.ValidTo
    yield! checkEvidence "transport_links" link.Slug sources link.Evidence
]

let private checkNotice places links festivals sources (notice: Notice) =
    let target =
        match notice.Scope with
        | NoticeScope.PlaceScope -> Some places
        | NoticeScope.LinkScope -> Some links
        | NoticeScope.FestivalScope -> Some festivals
        | NoticeScope.National
        | NoticeScope.State -> None

    [
        match target, notice.ScopeRef with
        | Some known, Some reference -> yield! checkReference "notices" notice.Slug known reference
        | Some _, None -> error "notices" notice.Slug "notice needs a scope reference"
        | None, _ -> ()
        yield! checkDates "notices" notice.Slug notice.ValidFrom notice.ValidTo
        yield! checkEvidence "notices" notice.Slug sources notice.Evidence
    ]

let private checkCard cards places illustrations photos (card: Card) = [
    for branch in card.Branches do
        yield! checkReference "card_branches" card.Slug cards branch.ToCard
    yield!
        card.PlaceSlug
        |> Option.toList
        |> List.collect (checkReference "cards" card.Slug places)
    yield!
        card.IllustrationSlug
        |> Option.toList
        |> List.collect (checkReference "cards" card.Slug illustrations)
    yield!
        card.PhotoSlug
        |> Option.toList
        |> List.collect (checkReference "cards" card.Slug photos)
]

let private checkPhoto places (photo: Photo) =
    let required = [
        photo.Credit, "photo needs a credit"
        photo.Licence, "photo needs a licence"
        photo.SourceUrl, "photo needs a source"
        photo.Alt, "photo needs alt text"
    ]

    [
        yield! checkReference "photos" photo.Slug places photo.PlaceSlug
        for value, message in required do
            if String.IsNullOrWhiteSpace value then
                error "photos" photo.Slug message
    ]

let private checkUnique table (slugs: string list) =
    slugs
    |> List.countBy id
    |> List.filter (fun (_, count) -> count > 1)
    |> List.map (fun (slug, _) -> error table slug "slug is used more than once")

let private bannedWords = [ "charas"; "ganja" ]

/// The product never recommends these, whatever a batch contains.
let private checkCopy (place: Place) =
    let copy = [
        place.Summary
        yield! Option.toList place.IsThisForYou
        yield! place.Experiences |> List.collect (fun item -> [ item.Name; item.Description ])
        yield! place.DayPlans |> List.map _.Plan
        yield!
            place.StayAreas
            |> List.collect (fun area -> area.Name :: Option.toList area.Note)
    ]

    [
        for word in bannedWords do
            if
                copy
                |> List.exists (fun text -> text.Contains(word, StringComparison.OrdinalIgnoreCase))
            then
                error "places" place.Slug $"visitor copy mentions banned word: {word}"
    ]

let private checkDishes (place: Place) = [
    for dish in place.Dishes do
        if dish.Vegan && not dish.Vegetarian then
            error "places" place.Slug $"{dish.Name} is vegan but not vegetarian"
]

/// Returns every problem in the catalog, not just the first.
let check (catalog: Catalog) : ValidationError list =
    let places = catalog.Places |> List.map _.Slug |> Set.ofList
    let links = catalog.Links |> List.map _.Slug |> Set.ofList
    let festivals = catalog.Festivals |> List.map _.Slug |> Set.ofList
    let cards = catalog.Cards |> List.map _.Slug |> Set.ofList
    let illustrations = catalog.Illustrations |> List.map _.Slug |> Set.ofList
    let photos = catalog.Photos |> List.map _.Slug |> Set.ofList
    let regions = catalog.Regions |> List.map _.Slug |> Set.ofList
    let clusters = catalog.Clusters |> List.map _.Slug |> Set.ofList
    let families = catalog.Families |> List.map _.Slug |> Set.ofList
    let interests = catalog.Interests |> List.map _.Slug |> Set.ofList
    let sources = catalog.Sources |> List.map _.Id |> Set.ofList

    [
        for cluster in catalog.Clusters do
            yield! checkReference "clusters" cluster.Slug regions cluster.RegionSlug
        yield! catalog.Places |> List.collect (checkPlace clusters families interests sources)
        yield! catalog.Links |> List.collect (checkLink places sources)
        yield! catalog.Notices |> List.collect (checkNotice places links festivals sources)
        yield! catalog.Cards |> List.collect (checkCard cards places illustrations photos)
        yield! catalog.Photos |> List.collect (checkPhoto places)
        yield! catalog.Places |> List.collect checkCopy
        yield! catalog.Places |> List.collect checkDishes
        yield! checkUnique "places" (catalog.Places |> List.map _.Slug)
        yield! checkUnique "transport_links" (catalog.Links |> List.map _.Slug)
        yield! checkUnique "cards" (catalog.Cards |> List.map _.Slug)
        yield! checkUnique "photos" (catalog.Photos |> List.map _.Slug)
        yield! checkUnique "notices" (catalog.Notices |> List.map _.Slug)
        for festival in catalog.Festivals do
            yield!
                festival.PlaceSlug
                |> Option.toList
                |> List.collect (checkReference "festivals" festival.Slug places)

            yield! checkEvidence "festivals" festival.Slug sources festival.Evidence
    ]
