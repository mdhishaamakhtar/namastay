module Namastay.Web.Server

open System
open System.Threading
open System.Threading.RateLimiting
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
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

    services.AddRateLimiter(fun options ->
        options.AddPolicy(Limits.tripsPolicy, Func<HttpContext, RateLimitPartition<string>>(partition))
        |> ignore

        options.OnRejected <- Func<OnRejectedContext, CancellationToken, ValueTask>(rejected))
    |> ignore

let useRateLimits (app: WebApplication) = app.UseRateLimiter() |> ignore

/// Turns any unexpected exception into a safe error and logs the real one with its trace ID.
let useErrorHandling (app: WebApplication) =
    let logger = app.Logger

    let handle (context: HttpContext) (next: RequestDelegate) : Task =
        task {
            try
                do! next.Invoke context
            with error ->
                logger.LogError(error, "Unhandled error for trace {TraceId}", context.TraceIdentifier)

                if not context.Response.HasStarted then
                    do! Errors.write context (ApiError.create ErrorCode.Internal "Something went wrong on our side.")
        }

    app.Use(Func<HttpContext, RequestDelegate, Task>(handle)) |> ignore

/// Unknown API addresses return the same error shape as every other failure.
let mapApiFallback (app: WebApplication) =
    let notFound (context: HttpContext) : Task =
        Errors.write context (ApiError.notFound "That address doesn't exist.")

    app.MapFallback("/api/{**path}", RequestDelegate notFound) |> ignore
