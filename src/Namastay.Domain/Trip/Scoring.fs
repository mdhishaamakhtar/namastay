module Namastay.Domain.Trip.Scoring

open Namastay.Domain.Catalog
open Namastay.Domain.Trip.Profile
open Namastay.Shared.Contracts

/// A liked card is worth more than a matching interest, and a second like more still,
/// but not so much that season, fit and budget stop mattering.
let private likeBonus = 150
let private mostLikeBonus = 300
let private declinedPenalty = 40
/// A parent or child of a liked interest counts for this share of it.
let private kinShare = 2
let private bestSeason = 8
let private unknownSeason = -2
let private fitWeight = 2
let private offbeatWeight = 1
let private budgetPenalty = 6

/// How many interests a trip of this length can honestly cover.
let interestLimit days =
    if days <= 7 then 2
    elif days <= 14 then 3
    else 4

let private budgetLevel =
    function
    | BudgetLevel.Budget -> 1
    | BudgetLevel.Mid -> 2
    | BudgetLevel.Luxury -> 3

let private related (parents: Map<string, string>) (one: string) (other: string) =
    parents.TryFind one = Some other || parents.TryFind other = Some one

/// What each of the place's interests is worth to the traveller: the interest itself,
/// or half of a parent or child they liked.
let private interestScore (profile: TravellerProfile) (interests: (string * int) list) (place: Place) =
    place.Interests
    |> List.sumBy (fun item ->
        interests
        |> List.map (fun (slug, score) ->
            if slug = item.InterestSlug then
                score * item.Weight
            elif related profile.Parents slug item.InterestSlug then
                score * item.Weight / kinShare
            else
                0)
        |> List.fold max 0)

let private fitScore (request: TripRequest) (place: Place) =
    place.TravellerFit
    |> List.tryFind (fun fit -> fit.Traveller = ContractMapping.traveller request.Travellers)
    |> Option.map (fun fit -> fit.Fit * fitWeight)
    |> Option.defaultValue 0

let private budgetScore (request: TripRequest) (place: Place) =
    let level = budgetLevel request.Budget

    match place.CostLow, place.CostHigh with
    | Some low, Some high when level < low || level > high -> -budgetPenalty
    | _ -> 0

let private popularityScore (profile: TravellerProfile) (place: Place) =
    match place.Popularity with
    | Popularity.Offbeat -> profile.Offbeat * offbeatWeight
    | Popularity.Between -> 0
    | Popularity.Mainstream -> if profile.Interests.IsEmpty then 2 else 0

/// How well a place suits the traveller, ignoring the season.
let taste (profile: TravellerProfile) (request: TripRequest) (place: Place) =
    let kept = profile.Interests |> List.truncate (interestLimit request.Days)

    let liked =
        profile.Likes.TryFind place.Slug
        |> Option.map (fun likes -> min mostLikeBonus (likes * likeBonus))
        |> Option.defaultValue 0

    let declined =
        if List.contains place.Slug profile.Declined then
            declinedPenalty
        else
            0

    interestScore profile kept place + liked - declined
    + fitScore request place
    + budgetScore request place
    + popularityScore profile place

let monthRating (month: int) (place: Place) =
    place.Months
    |> List.tryFind (fun item -> item.Month = month)
    |> Option.map _.Rating
    |> Option.defaultValue Rating.Unknown

let isUsableIn (month: int option) (place: Place) =
    match month with
    | None -> true
    | Some month ->
        match monthRating month place with
        | Rating.Avoid
        | Rating.Closed -> false
        | Rating.Best
        | Rating.Ok
        | Rating.Unknown -> true

let isClosedIn (month: int option) (place: Place) =
    month |> Option.exists (fun month -> monthRating month place = Rating.Closed)

let private seasonScore (month: int option) (place: Place) =
    match month with
    | None -> 0
    | Some month ->
        match monthRating month place with
        | Rating.Best -> bestSeason
        | Rating.Unknown -> unknownSeason
        | Rating.Ok
        | Rating.Avoid
        | Rating.Closed -> 0

let total profile (request: TripRequest) place =
    taste profile request place + seasonScore request.Month place
