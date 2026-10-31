module Namastay.Web.Server

open System
open System.Threading
open System.Threading.RateLimiting
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.AspNetCore.RateLimiting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Namastay.Shared.Contracts
open Namastay.Web.Http

let tripsPerMinute = Limits.tripsPerMinute

let private partition (context: HttpContext) =
    let address = context.Connection.RemoteIpAddress
    let key = if isNull address then "unknown" else address.ToString()

    RateLimitPartition.GetFixedWindowLimiter(
        key,
        fun _ ->
            FixedWindowRateLimiterOptions(
                PermitLimit = tripsPerMinute,
                Window = TimeSpan.FromMinutes 1.0,
                QueueLimit = 0
            )
    )

let private rejected (context: OnRejectedContext) (_: CancellationToken) =
    Errors.write
        context.HttpContext
        (ApiError.create ErrorCode.RateLimited "That's a lot of trips. Please wait a minute.")
    |> ValueTask

let configureServices (services: IServiceCollection) =
    services.AddLogging() |> ignore

    services.AddResponseCompression(fun options ->
        options.EnableForHttps <- true
        options.Providers.Add<BrotliCompressionProvider>()
        options.Providers.Add<GzipCompressionProvider>())
    |> ignore

    services.AddRateLimiter(fun options ->
        options.AddPolicy(Limits.tripsPolicy, Func<HttpContext, RateLimitPartition<string>>(partition))
        |> ignore

        options.OnRejected <- Func<OnRejectedContext, CancellationToken, ValueTask>(rejected))
    |> ignore

let useCompression (app: WebApplication) = app.UseResponseCompression() |> ignore

let useRateLimits (app: WebApplication) = app.UseRateLimiter() |> ignore

let private clientClosedRequest = 499

let private isApi (context: HttpContext) =
    context.Request.Path.StartsWithSegments(PathString "/api")

/// Routing answers unknown addresses and wrong methods with an empty response,
/// so API clients get our error shape here. Handlers that wrote their own error have started.
let private shapeEmptyError (context: HttpContext) =
    if not (isApi context) || context.Response.HasStarted then
        Task.CompletedTask
    else
        match context.Response.StatusCode with
        | StatusCodes.Status404NotFound -> Errors.write context (ApiError.notFound "That address doesn't exist.")
        | StatusCodes.Status405MethodNotAllowed ->
            Errors.write context (ApiError.create ErrorCode.MethodNotAllowed "That action isn't allowed here.")
        | _ -> Task.CompletedTask

/// Turns any unexpected exception into a safe error and logs the real one with its trace ID.
/// A visitor closing the page is not an error.
let guard (logger: ILogger) (context: HttpContext) (next: RequestDelegate) : Task =
    task {
        try
            do! next.Invoke context
            do! shapeEmptyError context
        with
        | :? OperationCanceledException when context.RequestAborted.IsCancellationRequested ->
            context.Response.StatusCode <- clientClosedRequest
        | error ->
            logger.LogError(error, "Unhandled error for trace {TraceId}", context.TraceIdentifier)

            if not context.Response.HasStarted then
                do! Errors.write context (ApiError.create ErrorCode.Internal "Something went wrong on our side.")
    }

let useErrorHandling (app: WebApplication) =
    app.Use(Func<HttpContext, RequestDelegate, Task>(guard app.Logger)) |> ignore
