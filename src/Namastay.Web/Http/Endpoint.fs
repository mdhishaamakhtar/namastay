module Namastay.Web.Http.Endpoint

open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
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

let private tryRead<'Request> (context: HttpContext) =
    task {
        try
            let! request = context.Request.ReadFromJsonAsync<'Request>(Json.options)
            return Ok request
        with :? JsonException ->
            return
                Error(
                    ApiError.validation [
                        {
                            Field = "body"
                            Message = "We couldn't read that request."
                        }
                    ]
                )
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
