/// One traveller swiping through the deck and getting a trip.
module Namastay.Bench.Session

open System
open Namastay.Bench.Personas
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Shared
open Namastay.Shared.Contracts

type Session = {
    Persona: Persona
    Shown: JourneyCard list
    Answers: CardAnswer list
    Trip: Result<Trip, Builder.TripError>
}

let private scopeOf (persona: Persona) =
    match persona.Scoped, persona.Interests with
    | true, first :: _ ->
        Some {
            Kind = ScopeKind.Feeling
            Slug = first
        }
    | _ -> None

let private request (persona: Persona) answers scope : TripRequest = {
    Answers = answers
    Days = persona.Days
    Month = persona.Month
    Travellers = persona.Travellers
    Budget = persona.Budget
    TransportComfort = persona.Comfort
    Scope = scope
    Diet = None
}

[<TailCall>]
let rec private swipe random (catalog: Catalog) deck persona answers =
    match JourneyPath.next deck answers with
    | None -> answers
    | Some slug ->
        let card = catalog.Cards |> List.find (fun item -> item.Slug = slug)
        let liked = Swipers.decide random catalog persona card
        swipe random catalog deck persona (answers @ [ { Card = slug; Liked = liked } ])

let play (catalog: Catalog) (graph: JourneyGraph) (persona: Persona) : Session =
    let random = Random persona.Seed
    let scope = scopeOf persona

    let allowed =
        scope |> Option.map (fun chosen -> Scope.feelingPlaces catalog chosen.Slug)

    let deck =
        match scope, allowed with
        | Some chosen, Some places -> Scope.within chosen places graph
        | _ -> graph

    let answers = swipe random catalog deck persona []

    {
        Persona = persona
        Shown =
            answers
            |> List.choose (fun answer -> deck.Cards |> List.tryFind (fun card -> card.Slug = answer.Card))
        Answers = answers
        Trip = Builder.buildWithin allowed catalog (request persona answers scope)
    }
