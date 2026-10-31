open System
open Microsoft.AspNetCore.Builder
open Npgsql
open Namastay.Data.Media
open Namastay.Web
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
    let app = builder.Build()
    Server.useCompression app
    Server.useErrorHandling app
    app.UseStaticFiles() |> ignore
    Server.useRateLimits app

    MediaRoute.map app (MediaStore.create settings.Media)
    use database = NpgsqlDataSource.Create settings.Database
    Server.mapHealth app database
    use data = DataCache.create database
    PlacesApi.map app data
    JourneyApi.map app data
    TripsApi.map app data
    GuideRoutes.map app data
    app.Run()
    0
