module Namastay.Web.Http.Errors

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Namastay.Shared.Contracts

let statusCode code =
    match code with
    | ErrorCode.ValidationFailed -> StatusCodes.Status400BadRequest
    | ErrorCode.Unauthorized -> StatusCodes.Status401Unauthorized
    | ErrorCode.Forbidden -> StatusCodes.Status403Forbidden
    | ErrorCode.NotFound -> StatusCodes.Status404NotFound
    | ErrorCode.Conflict -> StatusCodes.Status409Conflict
    | ErrorCode.RateLimited -> StatusCodes.Status429TooManyRequests
    | ErrorCode.Internal -> StatusCodes.Status500InternalServerError

let write (context: HttpContext) (error: ApiError) : Task =
    context.Response.StatusCode <- statusCode error.Code

    context.Response.WriteAsJsonAsync(
        {
            error with
                TraceId = context.TraceIdentifier
        },
        Json.options
    )
