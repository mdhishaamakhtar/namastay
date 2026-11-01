module Namastay.Web.Server

open System
open System.Threading
open System.Threading.RateLimiting
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.AspNetCore.RateLimiting
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Npgsql
open Namastay.Data
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Auth
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

let private configureSessions (site: Tokens.Site) (services: IServiceCollection) =
    services.AddSingleton site |> ignore

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(Tokens.configure site)
    |> ignore

/// The website calls this API from the visitor's browser, so only it may read answers
/// and send the sign-in cookie. Native apps aren't browsers, so this doesn't touch them.
let private configureWebsiteAccess (site: Tokens.Site) (services: IServiceCollection) =
    services.AddCors(fun options ->
        options.AddDefaultPolicy(fun policy ->
            policy.WithOrigins(site.Url).AllowCredentials().AllowAnyHeader().AllowAnyMethod()
            |> ignore))
    |> ignore

let configureServices (site: Tokens.Site) (services: IServiceCollection) =
    // The host's proxy is the only way in, so its forwarded headers can be trusted.
    // ForwardLimit stays at 1, so a visitor can't fake an address further back in the list.
    services.Configure<ForwardedHeadersOptions>(fun (options: ForwardedHeadersOptions) ->
        options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto
        options.KnownIPNetworks.Clear()
        options.KnownProxies.Clear())
    |> ignore

    services.AddLogging() |> ignore
    configureSessions site services
    configureWebsiteAccess site services

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

/// Tells browsers to keep this site out of frames and to trust the types it declares.
let private addSecurityHeaders (context: HttpContext) (next: RequestDelegate) : Task =
    let headers = context.Response.Headers
    headers.XContentTypeOptions <- "nosniff"
    headers.XFrameOptions <- "DENY"
    headers["Referrer-Policy"] <- "strict-origin-when-cross-origin"
    // The website is what search engines should list, not this address.
    headers["X-Robots-Tag"] <- "noindex"

    if context.Request.IsHttps then
        headers.StrictTransportSecurity <- "max-age=31536000"

    next.Invoke context

let useSecurityHeaders (app: WebApplication) =
    app.Use(Func<HttpContext, RequestDelegate, Task>(addSecurityHeaders)) |> ignore

let useCompression (app: WebApplication) = app.UseResponseCompression() |> ignore

let useForwardedHeaders (app: WebApplication) = app.UseForwardedHeaders() |> ignore

let useWebsiteAccess (app: WebApplication) = app.UseCors() |> ignore

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
