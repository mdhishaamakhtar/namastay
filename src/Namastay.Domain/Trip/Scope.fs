/// What a trip planned from a feeling, region or dish may use.
module Namastay.Domain.Trip.Scope

open Namastay.Domain.Catalog
open Namastay.Shared.Contracts

/// How strongly a place must suit a feeling to be offered under it.
let strongFit = 3

/// The places that suit a feeling strongly, which a trip planned from it may use.
let feelingPlaces (catalog: Catalog) (slug: string) =
    catalog.Places
    |> List.filter (fun place ->
        place.Published
        && place.Interests
           |> List.exists (fun item -> item.InterestSlug = slug && item.Weight >= strongFit))
    |> List.map _.Slug
    |> Set.ofList

/// A feeling deck this thin goes back to every card from the feeling's places.
let private thinDeck = 12

/// The feeling itself, or one of its children: quiet beaches speak for beaches.
let private speaksFor (interests: JourneyInterest list) (feeling: string) (card: JourneyCard) =
    card.Interests
    |> List.exists (fun item ->
        item.Slug = feeling
        || interests
           |> List.exists (fun interest -> interest.Slug = item.Slug && interest.Parent = Some feeling))

/// Keeps the cards a scoped trip can use. Food trips keep only the food cards, and a
/// feeling keeps the cards that speak for it, so a temples trip isn't swiped from forts.
let within (scope: TripScope) (places: Set<string>) (graph: JourneyGraph) =
    let atPlaces (card: JourneyCard) =
        card.Place |> Option.exists places.Contains

    let kept =
        match scope.Kind with
        | ScopeKind.FoodInRegion ->
            graph.Cards
            |> List.filter (fun card -> atPlaces card && card.Interest = Some "Food")
        | ScopeKind.Feeling ->
            let spoken =
                graph.Cards
                |> List.filter (fun card -> atPlaces card && speaksFor graph.Interests scope.Slug card)

            if spoken.Length >= thinDeck then
                spoken
            else
                graph.Cards |> List.filter atPlaces
        | ScopeKind.Region
        | ScopeKind.Dish -> graph.Cards |> List.filter atPlaces

    { graph with Cards = kept }
