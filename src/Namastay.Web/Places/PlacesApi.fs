module Namastay.Web.Places.PlacesApi

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Npgsql
open Namastay.Data.Places
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private maxQueryLength = 100

let private search source (context: HttpContext) : Task<Result<PlaceSummary list, ApiError>> =
    task {
        let query = (string context.Request.Query["query"]).Trim()

        if query.Length > maxQueryLength then
            return
                Error(
                    ApiError.validation [
                        {
                            Field = "query"
                            Message = "Please search with fewer words."
                        }
                    ]
                )
        elif Input.hasControlCharacters query then
            return
                Error(
                    ApiError.validation [
                        {
                            Field = "query"
                            Message = "Please use letters and numbers."
                        }
                    ]
                )
        elif query = "" then
            return Ok []
        else
            let! results = Search.run source query
            return Ok results
    }

let private guide source (context: HttpContext) : Task<Result<PlaceGuide, ApiError>> =
    task {
        let slug = string context.Request.RouteValues["slug"]

        let! found =
            if Input.isSlug slug then
                Guide.tryLoad source slug
            else
                Task.FromResult None

        return
            match found with
            | Some place -> Ok place
            | None -> Error(ApiError.notFound "We don't have a guide for that place yet.")
    }

let map (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    Endpoint.mapGet app Routes.searchPlaces (search source)
    Endpoint.mapGet app Routes.placeGuide (guide source)
