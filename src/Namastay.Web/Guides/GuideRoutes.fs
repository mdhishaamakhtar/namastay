module Namastay.Web.Guides.GuideRoutes

open System.Security
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Namastay.Data.Places
open Namastay.Shared
open Namastay.Web.Http

let private html (context: HttpContext) (status: int) (body: string) : Task =
    context.Response.StatusCode <- status
    context.Response.ContentType <- "text/html; charset=utf-8"
    context.Response.WriteAsync body

let private guide (data: DataCache) (context: HttpContext) : Task =
    task {
        let slug = string context.Request.RouteValues["slug"]
        let! version = DataCache.version data

        let! found =
            if Input.isSlug slug then
                DataCache.getOrLoadOption data $"page:{slug}" (fun () ->
                    task {
                        let! place = Guide.tryLoad data.Source slug
                        return place |> Option.map GuidePage.render
                    })
            else
                Task.FromResult None

        match found with
        | Some page ->
            if not (Endpoint.isFresh context version) then
                do! html context StatusCodes.Status200OK page
        | None -> do! html context StatusCodes.Status404NotFound (GuidePage.notFound ())
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
