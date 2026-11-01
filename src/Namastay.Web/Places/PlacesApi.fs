module Namastay.Web.Places.PlacesApi

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Namastay.Data.Places
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private maxQueryLength = 100

let private search (data: DataCache) (context: HttpContext) : Task<Result<PlaceSummary list, ApiError>> =
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
            let! results = Search.run data.Source query
            return Ok results
    }

let private guide (data: DataCache) (context: HttpContext) : Task<Result<PlaceGuide, ApiError>> =
    task {
        let slug = string context.Request.RouteValues["slug"]

        let! found =
            if Input.isSlug slug then
                DataCache.getOrLoadOption data $"guide:{slug}" (fun () -> Guide.tryLoad data.Source slug)
            else
                Task.FromResult None

        return
            match found with
            | Some place -> Ok place
            | None -> Error(ApiError.notFound "We don't have a guide for that place yet.")
    }

let private feelings (data: DataCache) (_: HttpContext) =
    task {
        let! all = DataCache.getOrLoad data "feelings" (fun () -> Feelings.all data.Source)
        return Ok all
    }

let private landing (data: DataCache) (_: HttpContext) =
    task {
        let! page = DataCache.getOrLoad data "landing" (fun () -> Landing.page data.Source)
        return Ok page
    }

let private feelingPlaces (data: DataCache) (context: HttpContext) =
    task {
        let slug = string context.Request.RouteValues["slug"]

        if Input.isSlug slug then
            let! places =
                DataCache.getOrLoad data $"feeling:{slug}" (fun () -> Feelings.places data.Source slug)

            return Ok places
        else
            return Ok []
    }

let map (app: IEndpointRouteBuilder) (data: DataCache) =
    Endpoint.mapData app data Routes.feelings (feelings data)
    Endpoint.mapData app data Routes.landing (landing data)
    Endpoint.mapData app data Routes.feelingPlaces (feelingPlaces data)
    Endpoint.mapGet app Routes.searchPlaces (search data)
    Endpoint.mapData app data Routes.placeGuide (guide data)
