module Namastay.Web.Trips.SavedTripsApi

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Namastay.Data.Accounts
open Namastay.Data.Catalog
open Namastay.Data.Trips
open Namastay.Domain.Trip
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Auth
open Namastay.Web.Http

let private signInFirst =
    ApiError.create ErrorCode.Unauthorized "Please sign in first."

let private notFound = ApiError.notFound "We couldn't find that trip."

/// The stop that gives a saved trip its colours and photo.
let private leadStop (trip: Trip) =
    trip.Stops
    |> List.tryFind (fun stop -> stop.FamilySlug = trip.BaseFamily)
    |> Option.orElse (List.tryHead trip.Stops)

let private summary (row: SavedTrips.SavedRow) =
    let trip = TripSnapshot.read row.Trip
    let stops = trip |> Option.map _.Stops |> Option.defaultValue []
    let lead = trip |> Option.bind leadStop

    {
        Id = row.Id.ToString()
        ShareToken = row.ShareToken
        Title = row.Title
        Days = stops |> List.sumBy _.Days
        SavedOn = row.SavedOn
        Route = stops |> List.map _.Name
        Photo = stops |> List.tryPick _.Photo
        Colours = lead |> Option.map _.Colours
    }

/// Runs `action` for the user behind a signed-in route. A valid token can outlive its account.
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

let private cantOpen =
    ApiError.create ErrorCode.Conflict "We can't open this saved trip. Please plan a new one."

/// Runs `action` on the signed-in user's own copy of the trip in the address.
let private withMine source (context: HttpContext) action =
    requireUser source context (fun user ->
        task {
            match tryParseId context with
            | None -> return Error notFound
            | Some id ->
                match! SavedTrips.tryFind source user.Id id with
                | None -> return Error notFound
                | Some row ->
                    match TripSnapshot.read row.Trip with
                    | None -> return Error cantOpen
                    | Some trip -> return! action user row trip
        })

let private fetch source (context: HttpContext) =
    withMine source context (fun _ row trip ->
        task {
            let! marked = TripsApi.markRetired source trip

            return
                Ok {
                    ShareToken = row.ShareToken
                    Trip = marked
                }
        })

let private blocked (reasons: string list) =
    let fields =
        reasons |> List.map (fun reason -> { Field = "stops"; Message = reason })

    {
        ApiError.create ErrorCode.Conflict "We can't save those changes." with
            Fields = fields
    }

let private edit (data: DataCache) (context: HttpContext) (changes: TripEdit) =
    withMine data.Source context (fun user row trip ->
        task {
            let! catalog = DataCache.getOrLoad data "catalog" (fun () -> Read.load data.Source)

            match Editing.apply catalog trip changes with
            | Error reasons -> return Error(blocked reasons)
            | Ok edited ->
                let text = JsonSerializer.Serialize(edited.Trip, Json.options)
                let! saved = SavedTrips.tryUpdate data.Source user.Id row.Id edited.Trip.Title text
                return if saved then Ok edited else Error notFound
        })

let map (app: IEndpointRouteBuilder) (data: DataCache) =
    let source = data.Source
    let signedIn = Endpoint.signedIn app
    Endpoint.mapAction signedIn Routes.saveItinerary (save source)
    Endpoint.mapGet signedIn Routes.myItineraries (list source)
    Endpoint.mapGet signedIn Routes.getMine (fetch source)
    Endpoint.mapAction signedIn Routes.deleteMine (delete source)

    (Endpoint.mapBody signedIn Routes.editMine (edit data)).RequireRateLimiting Limits.tripsPolicy
    |> ignore
