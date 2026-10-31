open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Npgsql
open Namastay.Data.Media
open Namastay.Web
open Namastay.Web.Media
open Namastay.Web.Places

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    Server.configureServices builder.Services
    let app = builder.Build()
    Server.useErrorHandling app

    app.MapGet("/health", Func<IResult>(fun () -> Results.Json {| status = "ok" |}))
    |> ignore

    let media = builder.Configuration.GetSection("Media")

    if not (isNull media["Endpoint"]) then
        MediaRoute.map
            app
            (MediaStore.create {
                Endpoint = media["Endpoint"]
                Bucket = media["Bucket"]
                AccessKey = media["AccessKey"]
                SecretKey = media["SecretKey"]
            })

    let database = builder.Configuration.GetConnectionString "Namastay"

    if not (isNull database) then
        PlacesApi.map app (NpgsqlDataSource.Create database)

    Server.mapApiFallback app
    app.Run()
    0
