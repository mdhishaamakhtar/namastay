open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Npgsql
open Namastay.Data.Media
open Namastay.Web
open Namastay.Web.Auth
open Namastay.Web.Media
open Namastay.Web.Guides
open Namastay.Web.Http
open Namastay.Web.Journey
open Namastay.Web.Places
open Namastay.Web.Trips

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    let settings =
        match Settings.load builder.Configuration with
        | Ok settings -> settings
        | Error missing ->
            let names = String.Join(", ", missing)
            raise (InvalidOperationException $"Missing settings: {names}")

    Server.configureServices builder.Services
    let google = Settings.google builder.Configuration

    google
    |> Option.iter (GoogleSignIn.configure (builder.Services.AddAuthentication()))

    let app = builder.Build()
    Server.useForwardedHeaders app
    Server.useCompression app
    Server.useErrorHandling app
    app.UseStaticFiles() |> ignore
    Server.useRateLimits app
    Server.useSessions app

    MediaRoute.map app (MediaStore.create settings.Media)
    use database = NpgsqlDataSource.Create settings.Database
    Server.mapHealth app database
    use data = DataCache.create database
    PlacesApi.map app data
    JourneyApi.map app data
    TripsApi.map app data
    GuideRoutes.map app data
    AuthApi.map app database
    AuthApi.mapOptions app database google.IsSome
    SavedTripsApi.map app data
    google |> Option.iter (fun _ -> GoogleSignIn.map app database)
    app.Run()
    0
