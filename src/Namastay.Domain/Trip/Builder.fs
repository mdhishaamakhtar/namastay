module Namastay.Domain.Trip.Builder

open System
open System.Globalization
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Domain.Trip.Gateways
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

let spareDay = "Free time to rest or explore at your own pace."
let travelDay = "A travel day. Arrive, settle in and take the evening slowly."

/// A journey this long takes up the day, so the plans start the day after.
let private fullDayJourney = 300

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

let private candidates (catalog: Catalog) profile (allowed: Set<string> option) =
    let excluded = catalog.Exclusions |> List.map _.Slug |> Set.ofList

    catalog.Places
    |> List.filter (fun place ->
        place.Published
        && not (excluded.Contains place.Slug)
        && not (isGated profile place)
        && allowed |> Option.forall (Set.contains place.Slug))

let private rank profile request (places: Place list) =
    places
    |> List.sortBy (fun place -> -(Scoring.total profile request place), place.Slug)

let private plansFor (place: Place) days (arrival: TransportLink option) =
    let variants = place.DayPlans |> List.groupBy _.Variant |> List.sortBy fst

    let travels =
        days > 1
        && arrival |> Option.exists (fun link -> link.MinutesMin >= fullDayJourney)

    let daysToPlan = if travels then days - 1 else days

    let chosen =
        variants
        |> List.map snd
        |> List.sortBy (fun plans -> abs (plans.Length - daysToPlan))
        |> List.tryHead
        |> Option.defaultValue []
        |> List.sortBy _.Day

    let plans = (if travels then [ travelDay ] else []) @ (chosen |> List.map _.Plan)

    [
        for day in 1..days ->
            {
                Day = day
                Plan = plans |> List.tryItem (day - 1) |> Option.defaultValue spareDay
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

let private dishesShown = 3
let private eateriesShown = 2

let private tripDish (dish: Dish) : TripDish = {
    Name = dish.Name
    Description = dish.Description
    Kind = DishKinds.ofFlags dish.Vegetarian dish.Vegan
}

/// The best-known dishes come first, and a diet only sees kitchens it can trust.
let private food (diet: Diet) (place: Place) =
    let dishes =
        place.Dishes
        |> List.map (fun dish -> dish, tripDish dish)
        |> List.filter (fun (_, shown) -> DishKinds.suits diet shown.Kind)
        |> List.sortBy (fun (dish, _) -> dish.Rank |> Option.defaultValue Int32.MaxValue)
        |> List.map snd
        |> List.truncate dishesShown

    let eateries =
        place.Eateries
        |> List.filter (fun eatery -> diet = Diet.Everything || eatery.VegetarianOnly)
        |> List.truncate eateriesShown
        |> List.map (fun eatery -> {
            Name = eatery.Name
            Area = eatery.Area
            VegetarianOnly = eatery.VegetarianOnly
        })

    if place.Dishes.IsEmpty && place.Eateries.IsEmpty then
        None
    else
        Some { Dishes = dishes; Eateries = eateries }

let toStop (catalog: Catalog) budget diet (stop: RoutedStop) : TripStop =
    let family =
        catalog.Families |> List.find (fun item -> item.Slug = stop.Place.FamilySlug)

    {
        PlaceSlug = stop.Place.Slug
        Name = plainName stop.Place
        Reason = None
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
        Plans = plansFor stop.Place stop.Days stop.Arrival
        Food = food diet stop.Place
        Arrival = stop.Arrival |> Option.map toLeg
        Retired = false
        Custom = false
    }

let private swaps (profile: TravellerProfile) (request: TripRequest) (places: Place list) (stops: RoutedStop list) =
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
let private mix (catalog: Catalog) (profile: TravellerProfile) =
    let top = profile.Interests |> List.truncate 3

    match top with
    | [] -> []
    | (_, strongest) :: _ ->
        top
        |> List.map (fun (slug, score) -> {
            Name = interestName catalog slug
            Share = score * 100 / strongest
        })

let private mostLeftOut = 3

let private leftOut (catalog: Catalog) (profile: TravellerProfile) (stops: RoutedStop list) =
    let covered =
        stops |> List.collect _.Place.Interests |> List.map _.InterestSlug |> Set.ofList

    let kept = profile.Interests |> List.map fst

    let chosen = stops |> List.map _.Place.Slug

    let places =
        catalog.Places
        |> List.filter (fun place ->
            List.contains place.Slug profile.Collected
            && not (List.contains place.Slug chosen))
        |> List.map plainName

    let interests =
        kept
        |> List.filter (fun slug -> not (covered.Contains slug))
        |> List.map (interestName catalog)

    List.truncate mostLeftOut places @ List.truncate mostLeftOut interests

let notices (catalog: Catalog) (month: int option) (stops: RoutedStop list) =
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

    let hardMonth (place: Place) =
        month
        |> Option.bind (fun month ->
            place.Months
            |> List.tryFind (fun item -> item.Month = month && item.Rating = Rating.Avoid)
            |> Option.map (fun item -> {
                TripNotice.PlaceSlug = Some place.Slug
                Kind = GuideNoticeKind.Season
                Severity = NoticeSeverity.Warning
                Body =
                    $"{place.Name} is hard going in {monthName month}. {item.Note |> Option.defaultValue String.Empty}"
                        .Trim()
            }))
        |> Option.toList

    stops
    |> List.collect (fun stop -> hardMonth stop.Place @ forPlace stop.Place @ permit stop.Place)

let bestMonths (stops: RoutedStop list) = [
    for month in 1..12 do
        if
            stops
            |> List.forall (fun stop -> Scoring.monthRating month stop.Place = Rating.Best)
        then
            month
]

/// "14 days of hindu temples and beaches in Tamil Nadu and Goa" for feelings, else just the places.
let private title (catalog: Catalog) (request: TripRequest) (stops: RoutedStop list) =
    let regions =
        stops
        |> List.map (fun stop ->
            catalog.Clusters
            |> List.tryFind (fun cluster -> cluster.Slug = stop.Place.ClusterSlug)
            |> Option.map _.RegionSlug
            |> Option.bind (fun region -> catalog.Regions |> List.tryFind (fun item -> item.Slug = region))
            |> Option.map _.Name
            |> Option.defaultValue stop.Place.State)
        |> List.distinct
        |> Sentence.listed

    match request.Scope with
    | Some {
               Kind = ScopeKind.Feeling
               Slugs = feelings
           } ->
        let names =
            feelings
            |> List.map (fun feeling -> (interestName catalog feeling).ToLowerInvariant())
            |> Sentence.listed

        $"{request.Days} days of {names} in {regions}"
    | _ -> $"{request.Days} days in {regions}"

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
    Arrive = arrive catalog stops
    Depart = depart catalog stops
    Stops =
        List.zip stops (Reasons.forStops catalog profile request stops)
        |> List.map (fun (stop, reason) -> {
            toStop catalog request.Budget (Option.defaultValue Diet.Everything request.Diet) stop with
                Reason = reason
        })
    Swaps = swaps profile request places stops
    Mix = mix catalog profile
    LeftOut = leftOut catalog profile stops
    Notices = notices catalog request.Month stops
    Month = request.Month
    BestMonths = bestMonths stops
    Budget = request.Budget
    Diet = request.Diet
    DataVersion = 1
}

/// Builds a realistic trip from rules and data alone. The same input gives the same trip.
/// `allowed` limits it to those places, for a trip planned from a region, feeling or dish.
let buildWithin (allowed: Set<string> option) (catalog: Catalog) (request: TripRequest) : Result<Trip, TripError> =
    if request.Days < RequestRules.minDays || request.Days > RequestRules.maxDays then
        Error(TripError.InvalidDays request.Days)
    else
        let profile = Profile.from catalog request
        let places = candidates catalog profile allowed
        // Someone who liked a place in a hard month gets it with a warning. Closed is closed.
        let usable =
            places
            |> List.filter (fun place ->
                Scoring.isUsableIn request.Month place
                || (List.contains place.Slug profile.Collected
                    && not (Scoring.isClosedIn request.Month place)))

        match usable with
        | [] -> Error TripError.NoPlaces
        | _ ->
            let stops =
                Routing.route
                    catalog
                    request
                    (profile.Depth > 0)
                    profile.Collected
                    places
                    (rank profile request usable)
                    (Scoring.marginal profile request)

            Ok(assemble catalog profile request places stops)

let build (catalog: Catalog) (request: TripRequest) = buildWithin None catalog request
