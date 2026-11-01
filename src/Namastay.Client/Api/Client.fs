/// The only code in the client that talks to the server.
module Namastay.Client.Api.Client

open Fable.Core
open Fable.Core.JsInterop
open Fetch
open Thoth.Json
open Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type ClientError =
    | Network
    | Server of ApiError
    | Unreadable of string

let readError (status: int) (text: string) =
    match Decode.Auto.fromString<ApiError>(text, caseStrategy = CamelCase) with
    | Ok error -> ClientError.Server error
    | Error _ -> ClientError.Unreadable $"The server answered {status} in an unknown format."

let inline decodeBody<'Response> (response: Response) =
    promise {
        let! text = response.text ()

        if response.Ok then
            return
                Decode.Auto.fromString<'Response>(text, caseStrategy = CamelCase)
                |> Result.mapError ClientError.Unreadable
        else
            return Error(readError response.Status text)
    }

let jsonHeaders =
    requestHeaders [ HttpRequestHeaders.ContentType "application/json" ]

let inline send<'Response> (url: string) (properties: RequestProperties list) =
    promise {
        try
            let! response = fetchUnsafe url properties
            return! decodeBody<'Response> response
        with _ ->
            return Error ClientError.Network
    }

let inline get<'Response> (url: string) : JS.Promise<Result<'Response, ClientError>> = send<'Response> url []

let inline post<'Request, 'Response> (url: string) (body: 'Request) : JS.Promise<Result<'Response, ClientError>> =
    let json =
        Encode.Auto.toString (0, body, caseStrategy = CamelCase, skipNullField = false)

    send<'Response> url [
        RequestProperties.Method HttpMethod.POST
        jsonHeaders
        RequestProperties.Body !^json
    ]

let inline put<'Request, 'Response> (url: string) (body: 'Request) : JS.Promise<Result<'Response, ClientError>> =
    let json =
        Encode.Auto.toString (0, body, caseStrategy = CamelCase, skipNullField = false)

    send<'Response> url [
        RequestProperties.Method HttpMethod.PUT
        jsonHeaders
        RequestProperties.Body !^json
    ]

/// A request whose answer has nothing to read, so only success matters.
let action (method: HttpMethod) (url: string) : JS.Promise<Result<unit, ClientError>> =
    promise {
        try
            let! response = fetchUnsafe url [ RequestProperties.Method method ]

            if response.Ok then
                return Ok()
            else
                let! text = response.text ()
                return Error(readError response.Status text)
        with _ ->
            return Error ClientError.Network
    }

let isSignedOut =
    function
    | ClientError.Server error -> error.Code = ErrorCode.Unauthorized
    | _ -> false

let private shortestSkeleton = 350

/// Keeps a skeleton up long enough to read as calm. A faster answer would look like a flicker.
let held (work: JS.Promise<'Result>) =
    promise {
        let pause = Promise.sleep shortestSkeleton
        let! result = work
        do! pause
        return result
    }

/// What a visitor should read, for any failure.
let describe =
    function
    | ClientError.Network -> "We can't reach Namastay right now. Check your connection and try again."
    | ClientError.Server error -> error.Message
    | ClientError.Unreadable _ -> "Something went wrong on our side. Please try again."
