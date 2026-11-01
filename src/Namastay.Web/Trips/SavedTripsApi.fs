module Namastay.Web.Trips.SavedTripsApi

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Npgsql
open Namastay.Data.Accounts
open Namastay.Data.Trips
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Auth
open Namastay.Web.Http

let private signInFirst =
    ApiError.create ErrorCode.Unauthorized "Please sign in first."

let private notFound = ApiError.notFound "We couldn't find that trip."

let private days (trip: string) =
    TripSnapshot.read trip
    |> Option.map (fun parsed -> parsed.Stops |> List.sumBy _.Days)
    |> Option.defaultValue 0

let private summary (row: SavedTrips.SavedRow) = {
    Id = row.Id.ToString()
    ShareToken = row.ShareToken
    Title = row.Title
    Days = days row.Trip
    SavedOn = row.SavedOn
}

/// Runs `action` for the signed-in user, or answers 401.
let requireUser source (context: HttpContext) (action: Users.UserRow -> Task<Result<'Value, ApiError>>) =
    task {
        let! user = AuthApi.currentUser source context

        match user with
        | Some found -> return! action found
        | None -> return Error signInFirst
    }

let tryParseId (context: HttpContext) =
    match Guid.TryParse(string context.Request.RouteValues["id"]) with
    | true, id -> Some id
    | _ -> None

let private save source (context: HttpContext) =
    requireUser source context (fun user ->
        task {
            let token = string context.Request.RouteValues["shareToken"]

            let! saved =
                if Input.isShareToken token then
                    SavedTrips.tryCopy source user.Id token
                else
                    Task.FromResult None

            return
                match saved with
                | Some row -> Ok(summary row)
                | None -> Error notFound
        })

let private list source (context: HttpContext) =
    requireUser source context (fun user ->
        task {
            let! rows = SavedTrips.listFor source user.Id
            return Ok(rows |> List.map summary)
        })

let private delete source (context: HttpContext) =
    requireUser source context (fun user ->
        task {
            match tryParseId context with
            | None -> return Error notFound
            | Some id ->
                let! removed = SavedTrips.delete source user.Id id
                return if removed then Ok() else Error notFound
        })

let map (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    Endpoint.mapAction app Routes.saveItinerary (save source)
    Endpoint.mapGet app Routes.myItineraries (list source)
    Endpoint.mapAction app Routes.deleteMine (delete source)
