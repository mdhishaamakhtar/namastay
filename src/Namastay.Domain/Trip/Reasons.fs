/// Why each stop is in a trip, in the one line the trip page shows under its name.
module Namastay.Domain.Trip.Reasons

open Namastay.Domain.Catalog
open Namastay.Domain.Trip.Profile
open Namastay.Domain.Trip.Routing
open Namastay.Shared.Contracts

let private titlesNamed = 2
/// An interest liked this often was kept choosing, not just liked once.
let private keptChoosing = 2

let closing = "The nearest city to fly home from"

/// The cards are named as the visitor saw them, so they're quoted.
let private named (titles: string list) =
    titles
    |> List.truncate titlesNamed
    |> List.map (fun title -> $"\u201C{title}\u201D")
    |> String.concat " and "

let private likedAt (profile: TravellerProfile) (place: Place) =
    profile.Liked
    |> List.filter (fun card -> card.PlaceSlug = Some place.Slug)
    |> List.map _.Title

/// How many liked cards spoke for an interest, or for a parent or child of it.
let private likesFor (profile: TravellerProfile) (slug: string) =
    profile.Liked
    |> List.filter (fun card ->
        Profile.interestsOf card
        |> List.exists (fun spoken -> spoken = slug || Scoring.related profile.Parents slug spoken))
    |> List.length

let private isPlannedAround (request: TripRequest) (slug: string) =
    match request.Scope with
    | Some {
               Kind = ScopeKind.Feeling
               Slugs = feelings
           } -> List.contains slug feelings
    | _ -> false

let private forInterest (catalog: Catalog) (profile: TravellerProfile) (request: TripRequest) (slug: string) =
    let name =
        catalog.Interests
        |> List.tryFind (fun interest -> interest.Slug = slug)
        |> Option.map _.Name
        |> Option.defaultValue slug

    if isPlannedAround request slug then
        Some $"{name}, which this trip is planned around"
    else
        match likesFor profile slug with
        | 0 -> None
        | likes when likes >= keptChoosing -> Some $"{name}, which you kept choosing"
        | _ -> Some $"{name}, which you liked"

let private ownReason (catalog: Catalog) (profile: TravellerProfile) (request: TripRequest) (stop: RoutedStop) =
    match likedAt profile stop.Place with
    | _ :: _ as titles -> Some $"Because you liked {named titles}"
    | [] when stop.Closing -> Some closing
    | [] ->
        Scoring.leadingInterest profile request stop.Place
        |> Option.bind (forInterest catalog profile request)

/// One reason for each stop, in order. The cards liked there come first, then the flight
/// home, then the interest the stop answers. A stop with none of those is on the way to
/// the next one, unless that's only the city to fly home from. It then has no reason,
/// rather than an invented one.
let forStops (catalog: Catalog) (profile: TravellerProfile) (request: TripRequest) (stops: RoutedStop list) =
    let onward = (stops |> List.tail |> List.map Some) @ [ None ]

    List.zip stops onward
    |> List.map (fun (stop, next) ->
        match ownReason catalog profile request stop, next with
        | Some reason, _ -> Some reason
        | None, Some following when not following.Closing -> Some $"On the way to {Gateways.plainName following.Place}"
        | None, _ -> None)
