/// The only code in the client that talks to the server.
module Namastay.Client.Api.Client

open Fable.Core
open Fable.Core.JsInterop
open Fetch
open Thoth.Json
open Namastay.Shared
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

/// Thoth builds a decoder from the type once, then reuses it for every answer.
let inline decode<'Value> (text: string) =
    let decoder = Decode.Auto.generateDecoderCached<'Value>(caseStrategy = CamelCase)
    Decode.fromString decoder text

let readError (status: int) (text: string) =
    match decode<ApiError> text with
    | Ok error -> ClientError.Server error
    | Error _ -> ClientError.Unreadable $"The server answered {status} in an unknown format."

/// Answers saved inside the page, and any public ones fetched since, so a page
/// already seen opens at once and then quietly catches up.
[<Emit("(window.__seed ?? {})[$0] ?? null")>]
let private seedText (url: string) : string = jsNative

[<Emit("(window.__seed ??= {})[$0] = $1")>]
let private rememberAnswer (url: string) (text: string) : unit = jsNative

/// Personal answers are never kept, so they can't outlive signing out.
let keepAnswer (url: string) (text: string) =
    if Routes.isPublicAnswer url then
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

            return decode<'Response> text |> Result.mapError ClientError.Unreadable
        else
            return Error(readError response.Status text)
    }

/// The website and the API are separate addresses, so the sign-in cookie must be asked for.
let withSignIn = RequestProperties.Credentials RequestCredentials.Include

let jsonHeaders =
    requestHeaders [ HttpRequestHeaders.ContentType "application/json" ]

let inline send (address: Address<'Request, 'Response>) (properties: RequestProperties list) =
    promise {
        try
            let! response = fetchUnsafe (fromApi address.Url) (withSignIn :: properties)
            return! decodeBody<'Response> address.Url response
        with _ ->
            return Error ClientError.Network
    }

let inline get (address: Address<NoBody, 'Response>) : JS.Promise<Result<'Response, ClientError>> = send address []

let inline private sendJson (method: HttpMethod) (address: Address<'Request, 'Response>) (body: 'Request) =
    let json =
        Encode.Auto.toString (0, body, caseStrategy = CamelCase, skipNullField = false)

    send address [ RequestProperties.Method method; jsonHeaders; RequestProperties.Body !^json ]

let inline post (address: Address<'Request, 'Response>) (body: 'Request) : JS.Promise<Result<'Response, ClientError>> =
    sendJson HttpMethod.POST address body

let inline put (address: Address<'Request, 'Response>) (body: 'Request) : JS.Promise<Result<'Response, ClientError>> =
    sendJson HttpMethod.PUT address body

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
let inline seeded (address: Address<NoBody, 'Response>) : 'Response option =
    match seedText address.Url with
    | null -> None
    | text -> decode<'Response> text |> Result.toOption

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
