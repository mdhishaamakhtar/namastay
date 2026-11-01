module Namastay.Web.Trips.TripsApi

open System.Security.Cryptography
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Npgsql
open Namastay.Data.Catalog
open Namastay.Data.Places
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

let private buildAndSave (data: DataCache) request =
    task {
        let! catalog = DataCache.getOrLoad data "catalog" (fun () -> Read.load data.Source)

        let! allowed =
            match request.Scope with
            | Some scope ->
                task {
                    let! places = Browse.placesIn data.Source scope
                    return Some places
                }
            | None -> Task.FromResult None

        match Builder.buildWithin allowed catalog request with
        | Error error -> return Error(toApiError error)
        | Ok trip ->
            let token = newToken ()

            do!
                Itineraries.save
                    data.Source
                    token
                    trip.Title
                    (JsonSerializer.Serialize(trip, Json.options))
                    trip.DataVersion

            return Ok { ShareToken = token; Trip = trip }
    }

let private create data (_: HttpContext) (request: TripRequest) : Task<Result<SavedTrip, ApiError>> =
    match RequestRules.check request with
    | [] -> buildAndSave data request
    | fields -> Task.FromResult(Error(ApiError.validation fields))

let markRetired source (trip: Trip) =
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

let private fetch (data: DataCache) (context: HttpContext) : Task<Result<SavedTrip, ApiError>> =
    task {
        let token = string context.Request.RouteValues["shareToken"]

        let! stored =
            if Input.isShareToken token then
                Itineraries.tryLoad data.Source token
            else
                Task.FromResult None

        match stored with
        | None -> return Error(ApiError.notFound "We couldn't find that trip.")
        | Some text ->
            match TripSnapshot.read text with
            | None ->
                return Error(ApiError.create ErrorCode.Conflict "We can't open this saved trip. Please plan a new one.")
            | Some trip ->
                let! marked = markRetired data.Source trip
                return Ok { ShareToken = token; Trip = marked }
    }

let map (app: IEndpointRouteBuilder) (data: DataCache) =
    (Endpoint.mapBody app Routes.createItinerary (create data)).RequireRateLimiting Limits.tripsPolicy
    |> ignore

    Endpoint.mapGet app Routes.getItinerary (fetch data)
