module Namastay.Web.Server

open System
open System.Threading
open System.Threading.RateLimiting
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.AspNetCore.RateLimiting
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Npgsql
open Namastay.Data
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let tripsPerMinute = Limits.tripsPerMinute

let private partition (limit: int) (context: HttpContext) =
    let address = context.Connection.RemoteIpAddress
    let key = if isNull address then "unknown" else address.ToString()

    RateLimitPartition.GetFixedWindowLimiter(
        key,
        fun _ -> FixedWindowRateLimiterOptions(PermitLimit = limit, Window = TimeSpan.FromMinutes 1.0, QueueLimit = 0)
    )

let private rejected (context: OnRejectedContext) (_: CancellationToken) =
    Errors.write
        context.HttpContext
        (ApiError.create ErrorCode.RateLimited "That's a lot of requests. Please wait a minute.")
    |> ValueTask

let private unauthorized (context: RedirectContext<CookieAuthenticationOptions>) : Task =
    context.Response.StatusCode <- StatusCodes.Status401Unauthorized
    Task.CompletedTask

let private configureSessions (services: IServiceCollection) =
    services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun options ->
            options.Cookie.Name <- "namastay"
            options.Cookie.HttpOnly <- true
            options.Cookie.SameSite <- SameSiteMode.Lax
            options.Cookie.SecurePolicy <- CookieSecurePolicy.SameAsRequest
            options.ExpireTimeSpan <- TimeSpan.FromDays 30.0
            options.SlidingExpiration <- true
            options.Events.OnRedirectToLogin <- Func<RedirectContext<CookieAuthenticationOptions>, Task>(unauthorized))
    |> ignore

let configureServices (services: IServiceCollection) =
    services.AddLogging() |> ignore
    configureSessions services

    services.AddResponseCompression(fun options ->
        options.EnableForHttps <- true
        options.Providers.Add<BrotliCompressionProvider>()
        options.Providers.Add<GzipCompressionProvider>())
    |> ignore

    services.AddRateLimiter(fun options ->
        options.AddPolicy(Limits.tripsPolicy, Func<HttpContext, RateLimitPartition<string>>(partition tripsPerMinute))
        |> ignore

        options.AddPolicy(
            Limits.signInPolicy,
            Func<HttpContext, RateLimitPartition<string>>(partition Limits.signInsPerMinute)
        )
        |> ignore

        options.OnRejected <- Func<OnRejectedContext, CancellationToken, ValueTask>(rejected))
    |> ignore

let useCompression (app: WebApplication) = app.UseResponseCompression() |> ignore

let useRateLimits (app: WebApplication) = app.UseRateLimiter() |> ignore

let useSessions (app: WebApplication) = app.UseAuthentication() |> ignore

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

/// Says whether this instance can serve visitors, unlike `/health`, which only says it's running.
let mapHealth (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    Endpoint.mapGet app Routes.health (fun _ -> Task.FromResult(Ok { Status = "ok" }))

    let ready (_: HttpContext) =
        task {
            try
                let! _ = Sql.query<int> source "select 1" null
                return Ok { Status = "ready" }
            with :? NpgsqlException ->
                return Error(ApiError.create ErrorCode.Unavailable "We're not ready yet. Please try again shortly.")
        }

    Endpoint.mapGet app Routes.ready ready
