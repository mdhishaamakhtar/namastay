open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Npgsql
open Namastay.Data.Media
open Namastay.Web
open Namastay.Web.Media
open Namastay.Web.Places

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

    app.MapGet("/health", Func<IResult>(fun () -> Results.Json {| status = "ok" |}))
    |> ignore

    MediaRoute.map app (MediaStore.create settings.Media)
    PlacesApi.map app (NpgsqlDataSource.Create settings.Database)
    Server.mapApiFallback app
    app.Run()
    0
