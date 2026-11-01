module Namastay.Web.Guides.GuideRoutes

open System.Security
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Namastay.Data.Places
open Namastay.Shared
open Namastay.Web.Auth
open Namastay.Web.Http

let private html (context: HttpContext) (status: int) (body: string) : Task =
    context.Response.StatusCode <- status
    context.Response.ContentType <- "text/html; charset=utf-8"
    context.Response.WriteAsync body

let private tripFrom (context: HttpContext) =
    let token = string context.Request.Query["trip"]
    if Input.isShareToken token then Some token else None

let private guide (data: DataCache) (context: HttpContext) : Task =
    task {
        let site = context.RequestServices.GetRequiredService<Tokens.Site>().Url
        let slug = string context.Request.RouteValues["slug"]
        let! version = DataCache.version data

        let! found =
            if Input.isSlug slug then
                DataCache.getOrLoadOption data $"guide:{slug}" (fun () -> Guide.tryLoad data.Source slug)
            else
                Task.FromResult None

        match found with
        | Some place ->
            if not (Endpoint.isFresh context version) then
                do! html context StatusCodes.Status200OK (GuidePage.render site (tripFrom context) place)
        | None -> do! html context StatusCodes.Status404NotFound (GuidePage.notFound site)
    }

let private sitemap (data: DataCache) (context: HttpContext) : Task =
    task {
        let! slugs = Guide.publishedSlugs data.Source
        let origin = $"{context.Request.Scheme}://{context.Request.Host}"

        let entries =
            slugs
            |> List.map (fun slug ->
                let path = Route.url [ "slug", slug ] [] Routes.guidePage
                $"<url><loc>{SecurityElement.Escape(origin + path)}</loc></url>")
            |> String.concat ""

        context.Response.ContentType <- "application/xml; charset=utf-8"

        do!
            context.Response.WriteAsync
                $"""<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">{entries}</urlset>"""
    }

let map (app: IEndpointRouteBuilder) (data: DataCache) =
    app.MapGet(Routes.guidePage.Template, RequestDelegate(guide data)) |> ignore
    app.MapGet(Routes.sitemap.Template, RequestDelegate(sitemap data)) |> ignore
