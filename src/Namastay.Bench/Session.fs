/// One traveller swiping through the deck and getting a trip.
module Namastay.Bench.Session

open System
open Namastay.Bench.Personas
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Shared
open Namastay.Shared.Contracts

/// Where the swipes settled, and the trip they'd have got by building then.
type EarlyFinish = {
    Cards: int
    Trip: Result<Trip, Builder.TripError>
}

type Session = {
    Persona: Persona
    Shown: JourneyCard list
    Answers: CardAnswer list
    Trip: Result<Trip, Builder.TripError>
    /// None when the swipes never settled before the deck ran out.
    Early: EarlyFinish option
}

let private scopeOf (persona: Persona) =
    match persona.Feelings with
    | [] -> None
    | feelings ->
        Some {
            Kind = ScopeKind.Feeling
            Slugs = feelings
        }

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
    match JourneyPath.next persona.Seed persona.Feelings deck answers with
    | None -> answers
    | Some slug ->
        let card = catalog.Cards |> List.find (fun item -> item.Slug = slug)
        let liked = Swipers.decide random catalog persona card
        swipe random catalog deck persona (answers @ [ { Card = slug; Liked = liked } ])

let play (catalog: Catalog) (graph: JourneyGraph) (persona: Persona) : Session =
    let random = Random persona.Seed
    let scope = scopeOf persona

    let allowed =
        scope |> Option.map (fun chosen -> Scope.feelingsPlaces catalog chosen.Slugs)

    let deck =
        match scope, allowed with
        | Some chosen, Some places -> Scope.within chosen places graph
        | _ -> graph

    let answers = swipe random catalog deck persona []

    let build swiped =
        Builder.buildWithin allowed catalog (request persona swiped scope)

    let early =
        [ 1 .. answers.Length - 1 ]
        |> List.tryFind (fun cards -> JourneyPath.settled deck (List.truncate cards answers))
        |> Option.map (fun cards -> {
            Cards = cards
            Trip = build (List.truncate cards answers)
        })

    {
        Persona = persona
        Shown =
            answers
            |> List.choose (fun answer -> deck.Cards |> List.tryFind (fun card -> card.Slug = answer.Card))
        Answers = answers
        Trip = build answers
        Early = early
    }
