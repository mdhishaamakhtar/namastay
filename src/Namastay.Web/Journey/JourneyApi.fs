module Namastay.Web.Journey.JourneyApi

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Namastay.Data.Journey
open Namastay.Data.Places
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private scopeFrom (context: HttpContext) : TripScope option =
    let slug = string context.Request.Query["of"]

    let kind =
        match string context.Request.Query["scope"] with
        | "region" -> Some ScopeKind.Region
        | "food" -> Some ScopeKind.FoodInRegion
        | "feeling" -> Some ScopeKind.Feeling
        | "dish" -> Some ScopeKind.Dish
        | _ -> None

    if Input.isSlug slug then
        kind |> Option.map (fun chosen -> { Kind = chosen; Slug = slug })
    else
        None

/// Keeps the cards a scoped trip can use. Food trips keep only the food cards.
let private within (scope: TripScope) (places: Set<string>) (graph: JourneyGraph) =
    let keep (card: JourneyCard) =
        card.Place |> Option.exists places.Contains
        && (scope.Kind <> ScopeKind.FoodInRegion || card.Interest = Some "Food")

    {
        graph with
            Cards = graph.Cards |> List.filter keep
    }

let private start data (context: HttpContext) : Task<Result<JourneyGraph, ApiError>> =
    task {
        let! graph = DataCache.getOrLoad data "journey" (fun () -> Graph.load data.Source)

        match scopeFrom context with
        | None -> return Ok graph
        | Some scope ->
            let! places = Browse.placesIn data.Source scope
            return Ok(within scope places graph)
    }

let map (app: IEndpointRouteBuilder) (data: DataCache) =
    Endpoint.mapData app data Routes.journeyStart (start data)
