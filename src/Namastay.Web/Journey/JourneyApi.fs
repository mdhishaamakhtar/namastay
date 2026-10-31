module Namastay.Web.Journey.JourneyApi

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Namastay.Data.Journey
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private start data (_: HttpContext) : Task<Result<JourneyGraph, ApiError>> =
    task {
        let! graph = DataCache.getOrLoad data "journey" (fun () -> Graph.load data.Source)
        return Ok graph
    }

let map (app: IEndpointRouteBuilder) (data: DataCache) =
    Endpoint.mapData app data Routes.journeyStart (start data)
