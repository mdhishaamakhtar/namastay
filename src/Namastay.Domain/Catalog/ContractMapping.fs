/// The one place where domain types and API contract types meet.
module Namastay.Domain.Catalog.ContractMapping

open Namastay.Shared
open Namastay.Shared.Contracts

let access =
    function
    | Gate.Open -> PlaceAccess.Open
    | Gate.Warn -> PlaceAccess.Warning
    | Gate.Permit -> PlaceAccess.Permit

let noticeKind =
    function
    | NoticeKind.Closure -> GuideNoticeKind.Closure
    | NoticeKind.Safety -> GuideNoticeKind.Safety
    | NoticeKind.Legal -> GuideNoticeKind.Legal
    | NoticeKind.Permit -> GuideNoticeKind.Permit

let severity =
    function
    | Severity.Info -> NoticeSeverity.Info
    | Severity.Warn -> NoticeSeverity.Warning
    | Severity.Permit -> NoticeSeverity.Permit
    | Severity.Danger -> NoticeSeverity.Danger

let travelMode =
    function
    | Mode.Flight -> TravelMode.Flight
    | Mode.Train -> TravelMode.Train
    | Mode.Road -> TravelMode.Road
    | Mode.Ferry -> TravelMode.Ferry
    | Mode.CableCar -> TravelMode.CableCar

let monthRating =
    function
    | Rating.Best -> MonthRating.Best
    | Rating.Ok -> MonthRating.Ok
    | Rating.Avoid -> MonthRating.Avoid
    | Rating.Closed -> MonthRating.Closed
    | Rating.Unknown -> MonthRating.Unknown

let travellerType =
    function
    | Traveller.Solo -> TravellerType.Solo
    | Traveller.Couple -> TravellerType.Couple
    | Traveller.Friends -> TravellerType.Friends
    | Traveller.Family -> TravellerType.Family

let traveller =
    function
    | TravellerType.Solo -> Traveller.Solo
    | TravellerType.Couple -> Traveller.Couple
    | TravellerType.Friends -> Traveller.Friends
    | TravellerType.Family -> Traveller.Family

let budgetLevel =
    function
    | CostLevel.Budget -> BudgetLevel.Budget
    | CostLevel.Mid -> BudgetLevel.Mid
    | CostLevel.Luxury -> BudgetLevel.Luxury

let costLevel =
    function
    | BudgetLevel.Budget -> CostLevel.Budget
    | BudgetLevel.Mid -> CostLevel.Mid
    | BudgetLevel.Luxury -> CostLevel.Luxury

let private interestPrefix = "interest:"

/// The interests a card speaks for, strongest first. Ties go to the first name alphabetically.
let private cardInterests (interests: Interest list) (card: Card) =
    card.Signals
    |> List.choose (fun signal ->
        interests
        |> List.tryFind (fun interest -> signal.Signal = interestPrefix + interest.Slug)
        |> Option.map (fun interest -> interest, signal.Weight))
    |> List.sortBy (fun (interest, weight) -> -weight, interest.Name)

let private placeColours (catalog: Catalog) (place: Place) =
    catalog.Families
    |> List.tryFind (fun family -> family.Slug = place.FamilySlug)
    |> Option.map (fun family -> {
        Deep = family.Deep
        Mid = family.Mid
        Pale = family.Pale
        TextOnDeep = family.TextOnDeep
    })

/// A photo fills the card and the illustration becomes its small mark.
/// Without a photo, the illustration fills the card.
let private journeyCard (catalog: Catalog) (known: Set<string>) (card: Card) : JourneyCard =
    let place =
        card.PlaceSlug
        |> Option.bind (fun slug -> catalog.Places |> List.tryFind (fun item -> item.Slug = slug))

    let photo =
        card.PhotoSlug
        |> Option.bind (fun slug -> catalog.Photos |> List.tryFind (fun item -> item.Slug = slug))

    let mark =
        card.IllustrationSlug
        |> Option.bind (fun slug -> catalog.Illustrations |> List.tryFind (fun item -> item.Slug = slug))

    let region =
        place
        |> Option.bind (fun item ->
            catalog.Clusters
            |> List.tryFind (fun cluster -> cluster.Slug = item.ClusterSlug))
        |> Option.map _.RegionSlug

    let interests = cardInterests catalog.Interests card

    {
        Slug = card.Slug
        Title = card.Title
        Line = card.Line
        IsStart = card.IsStart
        ImageUrl =
            photo
            |> Option.map _.FilePath
            |> Option.orElse (mark |> Option.map _.FilePath)
            |> Option.map Routes.mediaUrl
        ImageAlt =
            match photo with
            | Some found -> Some found.Alt
            | None -> mark |> Option.map _.Alt
        ImageCredit = photo |> Option.map _.Credit
        MarkUrl =
            if photo.IsSome then
                mark |> Option.map (_.FilePath >> Routes.mediaUrl)
            else
                None
        Interest = interests |> List.tryHead |> Option.map (fst >> _.Name)
        Interests =
            interests
            |> List.map (fun (interest, weight) -> {
                Slug = interest.Slug
                Weight = weight
            })
        Colours = place |> Option.bind (placeColours catalog)
        Branches =
            card.Branches
            |> List.filter (fun branch -> known.Contains branch.ToCard)
            |> List.sortBy (fun branch -> -branch.Priority, branch.ToCard)
            |> List.map (fun branch -> {
                OnAnswer = branch.OnAnswer
                ToCard = branch.ToCard
                Priority = branch.Priority
            })
        Place = card.PlaceSlug
        Region = region
    }

/// The swipe deck as the API serves it, from a catalog alone.
let journeyGraph (catalog: Catalog) : JourneyGraph =
    let known = catalog.Cards |> List.map _.Slug |> Set.ofList

    {
        Cards = catalog.Cards |> List.sortBy _.Slug |> List.map (journeyCard catalog known)
        Interests =
            catalog.Interests
            |> List.map (fun interest -> {
                Slug = interest.Slug
                Name = interest.Name
                Parent = interest.ParentSlug
            })
    }
