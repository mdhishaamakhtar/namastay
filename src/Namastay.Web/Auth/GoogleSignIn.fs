/// Only active when Google credentials are configured.
module Namastay.Web.Auth.GoogleSignIn

open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Google
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Npgsql
open Namastay.Data.Accounts
open Namastay.Shared

type GoogleSettings = {
    ClientId: string
    ClientSecret: string
}

let private externalScheme = "external"
let private finishedPath = "/auth/google/done"

let configure (builder: AuthenticationBuilder) (settings: GoogleSettings) =
    builder
        .AddCookie(externalScheme)
        .AddGoogle(fun options ->
            options.ClientId <- settings.ClientId
            options.ClientSecret <- settings.ClientSecret
            options.SignInScheme <- externalScheme)
    |> ignore

let private start (context: HttpContext) : Task =
    context.ChallengeAsync(GoogleDefaults.AuthenticationScheme, AuthenticationProperties(RedirectUri = finishedPath))

let private finish source (context: HttpContext) : Task =
    task {
        let site = context.RequestServices.GetRequiredService<Tokens.Site>()
        let! result = context.AuthenticateAsync externalScheme

        let email =
            if result.Succeeded then
                result.Principal.FindFirstValue ClaimTypes.Email
            else
                null

        if isNull email then
            context.Response.Redirect(site.Url + Routes.home.Template)
        else
            let! user = Users.claimForGoogle source email
            AuthApi.startSession context user
            do! context.SignOutAsync externalScheme
            context.Response.Redirect(site.Url + Routes.home.Template)
    }

let map (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    app.MapGet(Routes.googleSignIn.Template, RequestDelegate start) |> ignore
    app.MapGet(finishedPath, RequestDelegate(finish source)) |> ignore
