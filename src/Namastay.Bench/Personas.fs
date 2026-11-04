/// The synthetic travellers the scorecard plays through the planner.
module Namastay.Bench.Personas

open System
open Namastay.Domain.Catalog
open Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type Swiper =
    /// Likes the cards that match their interests, with a little noise.
    | Taste
    /// Flips a coin on every card.
    | Random
    /// Likes everything.
    | Everything

type Persona = {
    Name: string
    Swiper: Swiper
    Interests: string list
    Days: int
    Month: int option
    Travellers: TravellerType
    Budget: BudgetLevel
    Comfort: TransportComfort
    /// Planned from the first interest as a feeling, the way the feeling pages do.
    Scoped: bool
    Seed: int
}

let private tasteCount = 240
let private randomCount = 40
let private everythingCount = 20
let private dayOptions = [ 5; 7; 10; 14; 21 ]
let private interestPrefix = "interest:"

let private pick (random: Random) (items: 'T list) = items[random.Next items.Length]

/// Interests that at least one card carries, so a taste can be shown by swiping.
let private swipeable (catalog: Catalog) =
    let signalled =
        catalog.Cards |> List.collect _.Signals |> List.map _.Signal |> Set.ofList

    catalog.Interests
    |> List.filter (fun interest -> signalled.Contains(interestPrefix + interest.Slug))

let private shape (random: Random) swiper name interests scoped = {
    Name = name
    Swiper = swiper
    Interests = interests
    Days = pick random dayOptions
    Month = if random.Next 4 = 0 then None else Some(random.Next(1, 13))
    Travellers =
        pick random [
            TravellerType.Solo
            TravellerType.Couple
            TravellerType.Friends
            TravellerType.Family
        ]
    Budget = pick random [ BudgetLevel.Budget; BudgetLevel.Mid; BudgetLevel.Luxury ]
    Comfort =
        if random.Next 7 = 0 then
            TransportComfort.NoFlights
        else
            TransportComfort.Anything
    Scoped = scoped
    Seed = random.Next()
}

/// One or two unrelated interests. A fifth of those with a feeling plan from it.
let private taste (interests: Interest list) (random: Random) index =
    let first = pick random interests

    let unrelated =
        interests
        |> List.filter (fun other ->
            other.Slug <> first.Slug
            && other.ParentSlug <> Some first.Slug
            && Some other.Slug <> first.ParentSlug)

    let wanted =
        if unrelated.IsEmpty || random.Next 2 = 0 then
            [ first ]
        else
            [ first; pick random unrelated ]

    let scoped = first.ParentSlug.IsNone && random.Next 5 = 0
    shape random Swiper.Taste $"taste-{index}" (wanted |> List.map _.Slug) scoped

/// The same travellers every run, so two runs differ only by the planner.
let set (catalog: Catalog) (seed: int) : Persona list =
    let random = Random seed
    let interests = swipeable catalog

    [
        for index in 1..tasteCount do
            taste interests random index
        for index in 1..randomCount do
            shape random Swiper.Random $"random-{index}" [] false
        for index in 1..everythingCount do
            shape random Swiper.Everything $"everything-{index}" [] false
    ]
