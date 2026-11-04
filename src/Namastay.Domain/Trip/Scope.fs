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

/// Keeps the cards a scoped trip can use. Food trips keep only the food cards.
let within (scope: TripScope) (places: Set<string>) (graph: JourneyGraph) =
    let keep (card: JourneyCard) =
        card.Place |> Option.exists places.Contains
        && (scope.Kind <> ScopeKind.FoodInRegion || card.Interest = Some "Food")

    {
        graph with
            Cards = graph.Cards |> List.filter keep
    }
