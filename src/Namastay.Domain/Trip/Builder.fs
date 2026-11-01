module Namastay.Domain.Trip.Builder

open System.Globalization
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Domain.Trip.Profile
open Namastay.Domain.Trip.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type TripError =
    | InvalidDays of int
    | NoPlaces

type FamilyDays = {
    Slug: string
    Name: string
    Days: int
    Family: string
}

let spareDay = "A free day to rest or explore at your own pace."

/// The family with the most days leads. Ties go to the first one collected.
let baseFamily (collected: string list) (stops: FamilyDays list) =
    let order (family: string) =
        stops
        |> List.filter (fun stop -> stop.Family = family)
        |> List.choose (fun stop -> collected |> List.tryFindIndex (fun slug -> slug = stop.Slug))
        |> function
            | [] -> System.Int32.MaxValue
            | found -> List.min found

    stops
    |> List.groupBy _.Family
    |> List.map (fun (family, items) -> family, items |> List.sumBy _.Days)
    |> List.minBy (fun (family, days) -> -days, order family)
    |> fst

let monthName (month: int) =
    CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName month

let private isGated (profile: TravellerProfile) (place: Place) =
    place.Gate = Gate.Permit && not (List.contains place.Slug profile.Collected)

let private candidates (catalog: Catalog) profile (request: TripRequest) =
    let excluded = catalog.Exclusions |> List.map _.Slug |> Set.ofList

    catalog.Places
    |> List.filter (fun place ->
        place.Published
        && not (excluded.Contains place.Slug)
        && not (isGated profile place))

let private rank profile request (places: Place list) =
    places
    |> List.map (fun place -> place, Scoring.total profile request place)
    |> List.sortBy (fun (place, score) -> -score, place.Slug)

let private plansFor (place: Place) days =
    let variants = place.DayPlans |> List.groupBy _.Variant |> List.sortBy fst

    let chosen =
        variants
        |> List.map snd
        |> List.sortBy (fun plans -> abs (plans.Length - days))
        |> List.tryHead
        |> Option.defaultValue []
        |> List.sortBy _.Day

    [
        for day in 1..days ->
            {
                Day = day
                Plan =
                    chosen
                    |> List.tryItem (day - 1)
                    |> Option.map _.Plan
                    |> Option.defaultValue spareDay
            }
    ]

let private stayArea (budget: BudgetLevel) (place: Place) =
    let verified =
        place.StayAreas
        |> List.filter (fun area -> area.Evidence.Status <> FactStatus.Unverified)

    verified
    |> List.tryFind (fun area -> area.CostLevel = ContractMapping.costLevel budget)
    |> Option.orElse (verified |> List.tryHead)
    |> Option.map _.Name

let private leadPhoto (catalog: Catalog) (place: Place) =
    catalog.Photos
    |> List.filter (fun photo -> photo.PlaceSlug = place.Slug)
    |> List.sortBy _.Sort
    |> List.tryHead
    |> Option.map (fun photo -> {
        Url = Routes.mediaUrl photo.FilePath
        Alt = photo.Alt
        Credit = photo.Credit
        Licence = photo.Licence
        SourceUrl = photo.SourceUrl
    })

let toStop (catalog: Catalog) budget (stop: RoutedStop) : TripStop =
    let family =
        catalog.Families |> List.find (fun item -> item.Slug = stop.Place.FamilySlug)

    {
        PlaceSlug = stop.Place.Slug
        Name = stop.Place.Name
        Days = stop.Days
        FamilySlug = family.Slug
        Colours = {
            Deep = family.Deep
            Mid = family.Mid
            Pale = family.Pale
            TextOnDeep = family.TextOnDeep
        }
        Access = ContractMapping.access stop.Place.Gate
        StayArea = stayArea budget stop.Place
        Photo = leadPhoto catalog stop.Place
        Plans = plansFor stop.Place stop.Days
        Arrival =
            stop.Arrival
            |> Option.map (fun link -> {
                Mode = ContractMapping.travelMode link.Mode
                MinutesMin = link.MinutesMin
                MinutesMax = link.MinutesMax
                ComfortNote = link.ComfortNote
            })
        Retired = false
        Custom = false
    }

let private swaps profile (request: TripRequest) (places: Place list) (stops: RoutedStop list) =
    match request.Month, stops with
    | Some month, first :: _ when not profile.Interests.IsEmpty ->
        let chosen = stops |> List.map _.Place.Slug

        places
        |> List.sortBy (fun place -> -(Scoring.taste profile request place), place.Slug)
        |> List.truncate 3
        |> List.filter (fun place ->
            not (List.contains place.Slug chosen)
            && not (Scoring.isUsableIn (Some month) place)
            && Scoring.taste profile request place >= Scoring.taste profile request first.Place)
        |> List.map (fun place -> {
            From = place.Name
            To = first.Place.Name
            Reason = $"{place.Name} isn't a good choice in {monthName month}, so we chose {first.Place.Name} instead."
        })
    | _ -> []

let private interestName (catalog: Catalog) (slug: string) =
    catalog.Interests
    |> List.tryFind (fun interest -> interest.Slug = slug)
    |> Option.map _.Name
    |> Option.defaultValue slug

/// The strongest interests, each as a percentage of the strongest.
let private mix (catalog: Catalog) profile =
    let top = profile.Interests |> List.truncate 3

    match top with
    | [] -> []
    | (_, strongest) :: _ ->
        top
        |> List.map (fun (slug, score) -> {
            Name = interestName catalog slug
            Share = score * 100 / strongest
        })

let private leftOut (catalog: Catalog) profile (stops: RoutedStop list) =
    let covered =
        stops |> List.collect _.Place.Interests |> List.map _.InterestSlug |> Set.ofList

    let kept = profile.Interests |> List.map fst

    kept
    |> List.filter (fun slug -> not (covered.Contains slug))
    |> List.map (interestName catalog)

let notices (catalog: Catalog) (stops: RoutedStop list) =
    let forPlace (place: Place) =
        catalog.Notices
        |> List.filter (fun notice ->
            notice.Evidence.Status <> FactStatus.Unverified
            && notice.Scope = NoticeScope.PlaceScope
            && notice.ScopeRef = Some place.Slug)
        |> List.map (fun notice -> {
            TripNotice.PlaceSlug = Some place.Slug
            Kind = ContractMapping.noticeKind notice.Kind
            Severity = ContractMapping.severity notice.Severity
            Body = notice.Body
        })

    let permit (place: Place) =
        if place.Gate = Gate.Permit then
            [
                {
                    TripNotice.PlaceSlug = Some place.Slug
                    Kind = GuideNoticeKind.Permit
                    Severity = NoticeSeverity.Permit
                    Body = $"{place.Name} needs a permit. Sort this out before you travel."
                }
            ]
        else
            []

    stops |> List.collect (fun stop -> forPlace stop.Place @ permit stop.Place)

let bestMonths (stops: RoutedStop list) = [
    for month in 1..12 do
        if
            stops
            |> List.forall (fun stop -> Scoring.monthRating month stop.Place = Rating.Best)
        then
            month
]

let private title (catalog: Catalog) (request: TripRequest) (stops: RoutedStop list) =
    let names =
        stops
        |> List.map (fun stop ->
            catalog.Clusters
            |> List.tryFind (fun cluster -> cluster.Slug = stop.Place.ClusterSlug)
            |> Option.map _.RegionSlug
            |> Option.bind (fun region -> catalog.Regions |> List.tryFind (fun item -> item.Slug = region))
            |> Option.map _.Name
            |> Option.defaultValue stop.Place.State)
        |> List.distinct

    let joined = System.String.Join(" and ", names)
    $"{request.Days} days in {joined}"

let private assemble catalog profile (request: TripRequest) places stops : Trip = {
    Title = title catalog request stops
    BaseFamily =
        stops
        |> List.map (fun stop -> {
            Slug = stop.Place.Slug
            Name = stop.Place.Name
            Days = stop.Days
            Family = stop.Place.FamilySlug
        })
        |> baseFamily profile.Collected
    Stops = stops |> List.map (toStop catalog request.Budget)
    Swaps = swaps profile request places stops
    Mix = mix catalog profile
    LeftOut = leftOut catalog profile stops
    Notices = notices catalog stops
    Month = request.Month
    BestMonths = bestMonths stops
    Budget = request.Budget
    DataVersion = 1
}

/// Builds a realistic trip from rules and data alone. The same input gives the same trip.
let build (catalog: Catalog) (request: TripRequest) : Result<Trip, TripError> =
    if request.Days < RequestRules.minDays || request.Days > RequestRules.maxDays then
        Error(TripError.InvalidDays request.Days)
    else
        let profile = Profile.from catalog.Cards request
        let places = candidates catalog profile request
        let usable = places |> List.filter (Scoring.isUsableIn request.Month)

        match usable with
        | [] -> Error TripError.NoPlaces
        | _ ->
            let stops =
                Routing.route catalog request (profile.Depth > 0) (rank profile request usable)

            Ok(assemble catalog profile request places stops)
