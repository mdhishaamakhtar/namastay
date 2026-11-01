module Namastay.Web.Http.Endpoint

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.AspNetCore.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

let private respond (context: HttpContext) (result: Result<'Response, ApiError>) : Task =
    match result with
    | Ok body -> context.Response.WriteAsJsonAsync(body, Json.options)
    | Error error -> Errors.write context error

/// The route's types make the handler's result match the contract.
let mapGet
    (app: IEndpointRouteBuilder)
    (route: Route<NoBody, 'Response>)
    (handler: HttpContext -> Task<Result<'Response, ApiError>>)
    =
    let run (context: HttpContext) : Task =
        task {
            let! result = handler context
            do! respond context result
        }

    app.MapGet(route.Template, RequestDelegate run) |> ignore

/// A POST or DELETE with no body.
let mapAction
    (app: IEndpointRouteBuilder)
    (route: Route<NoBody, 'Response>)
    (handler: HttpContext -> Task<Result<'Response, ApiError>>)
    =
    let run (context: HttpContext) : Task =
        task {
            let! result = handler context
            do! respond context result
        }

    app.MapMethods(route.Template, [ route.Method ], RequestDelegate run) |> ignore

let private unreadable =
    ApiError.validation [
        {
            Field = "body"
            Message = "We couldn't read that request."
        }
    ]

let private limitBody (context: HttpContext) =
    match context.Features.Get<IHttpMaxRequestBodySizeFeature>() with
    | null -> ()
    | feature when feature.IsReadOnly -> ()
    | feature -> feature.MaxRequestBodySize <- Nullable Limits.maxBodyBytes

let private tryRead<'Request> (context: HttpContext) =
    task {
        if not (context.Request.HasJsonContentType()) then
            return Error(ApiError.create ErrorCode.UnsupportedMedia "Please send JSON.")
        elif context.Request.ContentLength.GetValueOrDefault 0L > Limits.maxBodyBytes then
            return Error(ApiError.create ErrorCode.TooLarge "That request is too large.")
        else
            limitBody context

            try
                let! request = context.Request.ReadFromJsonAsync<'Request>(Json.options)

                return
                    if isNull (box request) then
                        Error unreadable
                    else
                        Ok request
            with
            | :? JsonException -> return Error unreadable
            | :? BadHttpRequestException as error when error.StatusCode = StatusCodes.Status413PayloadTooLarge ->
                return Error(ApiError.create ErrorCode.TooLarge "That request is too large.")
    }

/// A body that can't be read becomes a validation error, never a crash.
let mapPost
    (app: IEndpointRouteBuilder)
    (route: Route<'Request, 'Response>)
    (handler: HttpContext -> 'Request -> Task<Result<'Response, ApiError>>)
    =
    let run (context: HttpContext) : Task =
        task {
            match! tryRead<'Request> context with
            | Error error -> do! Errors.write context error
            | Ok request ->
                let! result = handler context request
                do! respond context result
        }

    app.MapPost(route.Template, RequestDelegate run)

let private publicLifetime = "public, max-age=300"

/// True when the browser's copy is current. Otherwise marks the response cacheable.
let isFresh (context: HttpContext) (version: int64) =
    let tag = $"\"v{version}\""

    if context.Request.Headers.IfNoneMatch.ToString() = tag then
        context.Response.StatusCode <- StatusCodes.Status304NotModified
        true
    else
        context.Response.Headers.ETag <- tag
        context.Response.Headers.CacheControl <- publicLifetime
        false

/// A GET whose result depends only on the imported data.
let mapData
    (app: IEndpointRouteBuilder)
    (data: DataCache)
    (route: Route<NoBody, 'Response>)
    (handler: HttpContext -> Task<Result<'Response, ApiError>>)
    =
    let run (context: HttpContext) : Task =
        task {
            let! version = DataCache.version data

            if not (isFresh context version) then
                let! result = handler context

                match result with
                | Ok _ -> ()
                | Error _ -> context.Response.Headers.Remove "Cache-Control" |> ignore

                do! respond context result
        }

    app.MapGet(route.Template, RequestDelegate run) |> ignore
