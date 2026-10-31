module Namastay.Web.Journey.JourneyApi

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Npgsql
open Namastay.Data.Journey
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private start source (_: HttpContext) : Task<Result<JourneyGraph, ApiError>> =
    task {
        let! graph = Graph.load source
        return Ok graph
    }

let map (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    Endpoint.mapGet app Routes.journeyStart (start source)
