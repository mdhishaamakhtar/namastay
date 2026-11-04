/// How each kind of traveller answers a card.
module Namastay.Bench.Swipers

open System
open Namastay.Bench.Personas
open Namastay.Domain.Catalog

let private interestPrefix = "interest:"
let private likesMatch = 0.85
let private likesOther = 0.1

/// Wanted outright, or a child of something wanted. Quiet beaches suit someone who wants beaches.
let suits (interests: Interest list) (wanted: string list) (slug: string) =
    List.contains slug wanted
    || interests
       |> List.exists (fun interest ->
           interest.Slug = slug
           && interest.ParentSlug |> Option.exists (fun parent -> List.contains parent wanted))

let matches (catalog: Catalog) (wanted: string list) (card: Card) =
    card.Signals
    |> List.exists (fun signal ->
        signal.Signal.StartsWith interestPrefix
        && suits catalog.Interests wanted (signal.Signal.Substring interestPrefix.Length))

let decide (random: Random) (catalog: Catalog) (persona: Persona) (card: Card) =
    match persona.Swiper with
    | Swiper.Everything -> true
    | Swiper.Random -> random.Next 2 = 0
    | Swiper.Taste ->
        let chance =
            if matches catalog persona.Interests card then
                likesMatch
            else
                likesOther

        random.NextDouble() < chance
