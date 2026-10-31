module Namastay.Domain.Trip.Profile

open Namastay.Domain.Catalog
open Namastay.Shared.Contracts

type TravellerProfile = {
    Interests: (string * int) list
    Depth: int
    Offbeat: int
    Collected: string list
}

let private interestPrefix = "interest:"

let private likedCards (cards: Card list) (request: TripRequest) =
    request.Answers
    |> List.filter _.Liked
    |> List.choose (fun answer -> cards |> List.tryFind (fun card -> card.Slug = answer.Card))

let private signalTotal (signal: string) (cards: Card list) =
    cards
    |> List.collect _.Signals
    |> List.filter (fun item -> item.Signal = signal)
    |> List.sumBy _.Weight

let private rankedInterests (cards: Card list) =
    cards
    |> List.collect _.Signals
    |> List.filter _.Signal.StartsWith(interestPrefix)
    |> List.groupBy _.Signal
    |> List.map (fun (signal, items) -> signal.Substring interestPrefix.Length, items |> List.sumBy _.Weight)
    |> List.sortBy (fun (slug, score) -> -score, slug)

/// Turns swipes into a ranked mix of interests. Unknown cards are ignored.
let from (cards: Card list) (request: TripRequest) : TravellerProfile =
    let liked = likedCards cards request

    {
        Interests = rankedInterests liked
        Depth = signalTotal "depth" liked
        Offbeat = signalTotal "offbeat" liked
        Collected = liked |> List.choose _.PlaceSlug |> List.distinct
    }
