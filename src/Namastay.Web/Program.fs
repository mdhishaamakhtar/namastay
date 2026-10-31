open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Npgsql
open Namastay.Data.Media
open Namastay.Web
open Namastay.Web.Media
open Namastay.Web.Guides
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
    Server.useErrorHandling app
    Server.useRateLimits app

    app.MapGet("/health", Func<IResult>(fun () -> Results.Json {| status = "ok" |}))
    |> ignore

    MediaRoute.map app (MediaStore.create settings.Media)
    let database = NpgsqlDataSource.Create settings.Database
    PlacesApi.map app database
    JourneyApi.map app database
    TripsApi.map app database
    GuideRoutes.map app database
    Server.mapApiFallback app
    app.Run()
    0
