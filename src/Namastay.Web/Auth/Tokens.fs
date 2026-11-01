/// One signed token for every client. Native apps send it in a header,
/// and browsers hold it in a cookie that page scripts can't read.
module Namastay.Web.Auth.Tokens

open System
open System.Security.Claims
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Microsoft.IdentityModel.JsonWebTokens
open Microsoft.IdentityModel.Tokens

let cookieName = "namastay"
let lifetime = TimeSpan.FromDays 30.0
let private issuer = "namastay"
let private signing = SecurityAlgorithms.HmacSha256

/// Where this server signs tokens and which website it answers. Read from settings at startup.
type Site = { TokenKey: string; Url: string }

let private keyOf (site: Site) =
    SymmetricSecurityKey(Encoding.UTF8.GetBytes site.TokenKey)

let create (site: Site) (userId: Guid) =
    JsonWebTokenHandler()
        .CreateToken(
            SecurityTokenDescriptor(
                Issuer = issuer,
                Audience = issuer,
                Subject = ClaimsIdentity [ Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) ],
                Expires = DateTime.UtcNow.Add lifetime,
                SigningCredentials = SigningCredentials(keyOf site, signing)
            )
        )

let private fromCookie (context: MessageReceivedContext) =
    match context.Request.Cookies.TryGetValue cookieName with
    | true, token when context.Request.Headers.Authorization.Count = 0 -> context.Token <- token
    | _ -> ()

    Task.CompletedTask

let configure (site: Site) (options: JwtBearerOptions) =
    options.MapInboundClaims <- false

    options.TokenValidationParameters <-
        TokenValidationParameters(
            ValidIssuer = issuer,
            ValidAudience = issuer,
            IssuerSigningKey = keyOf site,
            ValidAlgorithms = [ signing ],
            ClockSkew = TimeSpan.FromMinutes 1.0
        )

    options.Events <- JwtBearerEvents(OnMessageReceived = Func<_, _> fromCookie)

let private cookieOptions (context: HttpContext) =
    CookieOptions(HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps, Path = "/")

let setCookie (context: HttpContext) (token: string) =
    let options = cookieOptions context
    options.Expires <- DateTimeOffset.UtcNow.Add lifetime
    context.Response.Cookies.Append(cookieName, token, options)

let clearCookie (context: HttpContext) =
    context.Response.Cookies.Delete(cookieName, cookieOptions context)
