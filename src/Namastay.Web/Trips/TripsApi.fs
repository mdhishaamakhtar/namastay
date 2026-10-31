module Namastay.Web.Trips.TripsApi

open System.Security.Cryptography
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Npgsql
open Namastay.Data.Catalog
open Namastay.Data.Trips
open Namastay.Domain.Trip
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private tokenAlphabet =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-"

let private tokenLength = 12

let private newToken () =
    RandomNumberGenerator.GetString(tokenAlphabet.ToCharArray(), tokenLength)

let private toApiError error =
    match error with
    | Builder.TripError.InvalidDays _ ->
        ApiError.validation [
            {
                Field = "days"
                Message = "Please choose between 5 and 21 days."
            }
        ]
    | Builder.TripError.NoPlaces ->
        ApiError.create ErrorCode.Conflict "We couldn't build a trip from those choices. Please try different dates."

let private create source (_: HttpContext) (request: TripRequest) : Task<Result<SavedTrip, ApiError>> =
    task {
        let! catalog = Read.load source

        match Builder.build catalog request with
        | Error error -> return Error(toApiError error)
        | Ok trip ->
            let token = newToken ()
            do! Itineraries.save source token trip.Title (JsonSerializer.Serialize(trip, Json.options)) trip.DataVersion
            return Ok { ShareToken = token; Trip = trip }
    }

let private markRetired source (trip: Trip) =
    task {
        let! retired = Itineraries.retiredAmong source (trip.Stops |> List.map _.PlaceSlug)

        return {
            trip with
                Stops =
                    trip.Stops
                    |> List.map (fun stop -> {
                        stop with
                            Retired = List.contains stop.PlaceSlug retired
                    })
        }
    }

let private fetch source (context: HttpContext) : Task<Result<SavedTrip, ApiError>> =
    task {
        let token = string context.Request.RouteValues["shareToken"]
        let! stored = Itineraries.tryLoad source token

        match stored with
        | None -> return Error(ApiError.notFound "We couldn't find that trip.")
        | Some text ->
            let! trip = markRetired source (JsonSerializer.Deserialize<Trip>(text, Json.options))
            return Ok { ShareToken = token; Trip = trip }
    }

let map (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    (Endpoint.mapPost app Routes.createItinerary (create source)).RequireRateLimiting Limits.tripsPolicy
    |> ignore

    Endpoint.mapGet app Routes.getItinerary (fetch source)
