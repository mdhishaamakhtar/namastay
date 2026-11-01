module Namastay.Web.Auth.AuthApi

open System
open System.Net.Mail
open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.JsonWebTokens
open Npgsql
open Namastay.Data.Accounts
open Namastay.Data.Places
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private minPassword = 10
let private maxPassword = 128
let private maxEmail = 254
let private wrongDetails = "That email or password isn't right."

let private hasher = PasswordHasher<string>()

/// Checked against when an account doesn't exist, so a wrong email takes as long as a wrong password.
let private decoyHash = hasher.HashPassword("", Guid.NewGuid().ToString())

let private isEmail (text: string) =
    let parsed, _ = MailAddress.TryCreate text

    parsed
    && text.Length <= maxEmail
    && not (text.Contains ' ')
    && text.LastIndexOf '.' > text.LastIndexOf '@'

let private check (credentials: Credentials) = [
    if not (isEmail credentials.Email) then
        {
            Field = "email"
            Message = "Please enter a valid email address."
        }
    if
        credentials.Password.Length < minPassword
        || credentials.Password.Length > maxPassword
    then
        {
            Field = "password"
            Message = $"Please use {minPassword} to {maxPassword} characters."
        }
]

let private toAccount (user: Users.UserRow) : Account = {
    Email = user.Email
    Provider =
        if user.Provider = "google" then
            SignInProvider.Google
        else
            SignInProvider.Password
}

let private siteOf (context: HttpContext) =
    context.RequestServices.GetRequiredService<Tokens.Site>()

/// Signs a browser in. Used by every web sign-in route, Google included.
let startSession (context: HttpContext) (user: Users.UserRow) =
    Tokens.setCookie context (Tokens.create (siteOf context) user.Id)

let private matches (user: Users.UserRow option) (password: string) =
    let hash = user |> Option.bind _.PasswordHash |> Option.defaultValue decoyHash

    let result = hasher.VerifyHashedPassword("", hash, password)

    user.IsSome
    && user.Value.PasswordHash.IsSome
    && result <> PasswordVerificationResult.Failed

let private signUp source (context: HttpContext) (credentials: Credentials) : Task<Result<Account, ApiError>> =
    task {
        match check credentials with
        | [] ->
            let hash = hasher.HashPassword("", credentials.Password)

            let! created =
                Users.tryCreate source (credentials.Email.Trim()) "password" (Some hash)

            match created with
            | Some user ->
                startSession context user
                return Ok(toAccount user)
            | None -> return Error(ApiError.create ErrorCode.Conflict "That email already has an account.")
        | fields -> return Error(ApiError.validation fields)
    }

let private signIn source (context: HttpContext) (credentials: Credentials) : Task<Result<Account, ApiError>> =
    task {
        let! user = Users.tryFindByEmail source (credentials.Email.Trim())

        if matches user credentials.Password then
            startSession context user.Value
            return Ok(toAccount user.Value)
        else
            return Error(ApiError.create ErrorCode.Unauthorized wrongDetails)
    }

/// Native apps sign in here, and get the token back instead of a cookie.
let private issueToken source (context: HttpContext) (credentials: Credentials) : Task<Result<TokenGrant, ApiError>> =
    task {
        let! user = Users.tryFindByEmail source (credentials.Email.Trim())

        if matches user credentials.Password then
            return
                Ok {
                    Token = Tokens.create (siteOf context) user.Value.Id
                    Account = toAccount user.Value
                }
        else
            return Error(ApiError.create ErrorCode.Unauthorized wrongDetails)
    }

let private signOut (context: HttpContext) : Task =
    Tokens.clearCookie context
    Task.CompletedTask

/// The signed-in user, if any. Other features use this to know who is asking.
let currentUser source (context: HttpContext) : Task<Users.UserRow option> =
    task {
        match Guid.TryParse(context.User.FindFirstValue JwtRegisteredClaimNames.Sub) with
        | true, id -> return! Users.tryFindById source id
        | _ -> return None
    }

let private me source (context: HttpContext) : Task<Result<Account, ApiError>> =
    task {
        let! user = currentUser source context

        return
            match user with
            | Some found -> Ok(toAccount found)
            | None -> Error(ApiError.create ErrorCode.Unauthorized "Please sign in.")
    }

/// Tells the sign-in page which ways of signing in this server offers.
let mapOptions (app: IEndpointRouteBuilder) source (google: bool) =
    Endpoint.mapGet app Routes.signInOptions (fun _ ->
        task {
            let! photo = LeadPhoto.tryFind source
            return Ok { Google = google; Photo = photo }
        })

let map (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    (Endpoint.mapBody app Routes.signUp (signUp source)).RequireRateLimiting Limits.signInPolicy
    |> ignore

    (Endpoint.mapBody app Routes.signIn (signIn source)).RequireRateLimiting Limits.signInPolicy
    |> ignore

    (Endpoint.mapBody app Routes.issueToken (issueToken source)).RequireRateLimiting Limits.signInPolicy
    |> ignore

    app.MapPost(Routes.signOut.Template, RequestDelegate signOut) |> ignore
    Endpoint.mapGet app Routes.me (me source)
