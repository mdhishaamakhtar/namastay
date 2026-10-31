open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    app.MapGet("/health", Func<IResult>(fun () -> Results.Json {| status = "ok" |})) |> ignore

    app.Run()

    0 // Exit code

