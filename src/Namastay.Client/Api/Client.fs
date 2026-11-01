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

/// Where the API lives. Empty in development, where Vite passes calls along.
[<Emit("import.meta.env.VITE_API_URL ?? ''")>]
let apiUrl: string = jsNative

/// The full address of something the API serves, such as a photo or a guide.
let fromApi (path: string) = apiUrl + path

let readError (status: int) (text: string) =
    match Decode.Auto.fromString<ApiError>(text, caseStrategy = CamelCase) with
    | Ok error -> ClientError.Server error
    | Error _ -> ClientError.Unreadable $"The server answered {status} in an unknown format."

/// Answers saved inside the page, and any public ones fetched since, so a page
/// already seen opens at once and then quietly catches up.
[<Emit("(window.__seed ?? {})[$0] ?? null")>]
let private seedText (url: string) : string = jsNative

[<Emit("(window.__seed ??= {})[$0] = $1")>]
let private rememberAnswer (url: string) (text: string) : unit = jsNative

/// Personal answers are never kept, so they can't outlive signing out.
let private isPublic (url: string) =
    not (
        url.StartsWith "/api/my"
        || url.StartsWith "/api/me"
        || url.StartsWith "/api/itineraries"
    )

let keepAnswer (url: string) (text: string) =
    if isPublic url then
        rememberAnswer url text

[<Emit("(() => { try { return localStorage.getItem('namastay-signed-in') === 'yes'; } catch { return false; } })()")>]
let wasSignedIn () : bool = jsNative

[<Emit("(() => { try { $0 ? localStorage.setItem('namastay-signed-in', 'yes') : localStorage.removeItem('namastay-signed-in'); } catch {} })()")>]
let rememberSignedIn (signedIn: bool) : unit = jsNative

let inline decodeBody<'Response> (url: string) (response: Response) =
    promise {
        let! text = response.text ()

        if response.Ok then
            keepAnswer url text

            return
                Decode.Auto.fromString<'Response>(text, caseStrategy = CamelCase)
                |> Result.mapError ClientError.Unreadable
        else
            return Error(readError response.Status text)
    }

/// The website and the API are separate addresses, so the sign-in cookie must be asked for.
let withSignIn = RequestProperties.Credentials RequestCredentials.Include

let jsonHeaders =
    requestHeaders [ HttpRequestHeaders.ContentType "application/json" ]

let inline send<'Response> (url: string) (properties: RequestProperties list) =
    promise {
        try
            let! response = fetchUnsafe (fromApi url) (withSignIn :: properties)
            return! decodeBody<'Response> url response
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
            let! response =
                fetchUnsafe (fromApi url) [ withSignIn; RequestProperties.Method method ]

            if response.Ok then
                return Ok()
            else
                let! text = response.text ()
                return Error(readError response.Status text)
        with _ ->
            return Error ClientError.Network
    }

let isNotFound =
    function
    | ClientError.Server error -> error.Code = ErrorCode.NotFound
    | _ -> false

let isSignedOut =
    function
    | ClientError.Server error -> error.Code = ErrorCode.Unauthorized
    | _ -> false

/// What's already known about `url`, so it needn't wait for the API.
let inline seeded<'Response> (url: string) : 'Response option =
    match seedText url with
    | null -> None
    | text ->
        Decode.Auto.fromString<'Response>(text, caseStrategy = CamelCase)
        |> Result.toOption

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
