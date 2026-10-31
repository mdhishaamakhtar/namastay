module Namastay.Web.Http.Endpoint

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
