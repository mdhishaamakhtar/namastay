module Namastay.Domain.Trip.Profile

open Namastay.Domain.Catalog
open Namastay.Shared.Contracts

type TravellerProfile = {
    Interests: (string * int) list
    /// Each interest's parent, so a child counts towards it and the other way round.
    Parents: Map<string, string>
    Depth: int
    Offbeat: int
    /// The liked cards, first like first.
    Liked: Card list
    /// Places whose cards were liked, first like first.
    Collected: string list
    /// How many cards were liked at each collected place.
    Likes: Map<string, int>
    /// Places whose every shown card was passed.
    Declined: string list
}

let private interestPrefix = "interest:"

let private answeredCards (cards: Card list) (request: TripRequest) =
    request.Answers
    |> List.choose (fun answer ->
        cards
        |> List.tryFind (fun card -> card.Slug = answer.Card)
        |> Option.map (fun card -> card, answer.Liked))

let private signalTotal (signal: string) (cards: Card list) =
    cards
    |> List.collect _.Signals
    |> List.filter (fun item -> item.Signal = signal)
    |> List.sumBy _.Weight

/// The interests a card speaks for.
let interestsOf (card: Card) =
    card.Signals
    |> List.filter _.Signal.StartsWith(interestPrefix)
    |> List.map (fun item -> item.Signal.Substring interestPrefix.Length)

let private rankedInterests (cards: Card list) =
    cards
    |> List.collect _.Signals
    |> List.filter _.Signal.StartsWith(interestPrefix)
    |> List.groupBy _.Signal
    |> List.map (fun (signal, items) -> signal.Substring interestPrefix.Length, items |> List.sumBy _.Weight)
    |> List.sortBy (fun (slug, score) -> -score, slug)

/// A feeling the trip was planned from leads the mix, ahead of whatever the swipes said.
let private lead (scope: TripScope option) (ranked: (string * int) list) =
    match scope with
    | Some {
               Kind = ScopeKind.Feeling
               Slug = feeling
           } ->
        let strongest = ranked |> List.map snd |> List.fold max 0

        (feeling, strongest + 1)
        :: (ranked |> List.filter (fun (slug, _) -> slug <> feeling))
    | _ -> ranked

/// Turns swipes into a ranked mix of interests. Unknown cards are ignored.
let from (catalog: Catalog) (request: TripRequest) : TravellerProfile =
    let answered = answeredCards catalog.Cards request
    let liked = answered |> List.filter snd |> List.map fst
    let likes = liked |> List.choose _.PlaceSlug |> List.countBy id
    let collected = likes |> List.map fst

    {
        Interests = lead request.Scope (rankedInterests liked)
        Parents =
            catalog.Interests
            |> List.choose (fun interest -> interest.ParentSlug |> Option.map (fun parent -> interest.Slug, parent))
            |> Map.ofList
        Liked = liked
        Depth = signalTotal "depth" liked
        Offbeat = signalTotal "offbeat" liked
        Collected = collected
        Likes = Map.ofList likes
        Declined =
            answered
            |> List.choose (fun (card, wasLiked) -> if wasLiked then None else card.PlaceSlug)
            |> List.distinct
            |> List.filter (fun slug -> not (List.contains slug collected))
    }
