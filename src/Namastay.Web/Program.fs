open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Namastay.Web

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    Server.configureServices builder.Services
    let app = builder.Build()
    Server.useErrorHandling app

    app.MapGet("/health", Func<IResult>(fun () -> Results.Json {| status = "ok" |}))
    |> ignore

    Server.mapApiFallback app
    app.Run()
    0
