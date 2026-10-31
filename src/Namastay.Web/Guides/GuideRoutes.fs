module Namastay.Web.Guides.GuideRoutes

open System.Security
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Npgsql
open Namastay.Data.Places

let private html (context: HttpContext) (status: int) (body: string) : Task =
    context.Response.StatusCode <- status
    context.Response.ContentType <- "text/html; charset=utf-8"
    context.Response.WriteAsync body

let private guide source (context: HttpContext) : Task =
    task {
        let! found = Guide.tryLoad source (string context.Request.RouteValues["slug"])

        match found with
        | Some place -> do! html context StatusCodes.Status200OK (GuidePage.render place)
        | None -> do! html context StatusCodes.Status404NotFound (GuidePage.notFound ())
    }

let private sitemap source (context: HttpContext) : Task =
    task {
        let! slugs = Guide.publishedSlugs source
        let origin = $"{context.Request.Scheme}://{context.Request.Host}"

        let entries =
            slugs
            |> List.map (fun slug ->
                $"<url><loc>{SecurityElement.Escape origin}/places/{SecurityElement.Escape slug}</loc></url>")
            |> String.concat ""

        context.Response.ContentType <- "application/xml; charset=utf-8"

        do!
            context.Response.WriteAsync
                $"""<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">{entries}</urlset>"""
    }

let map (app: IEndpointRouteBuilder) (source: NpgsqlDataSource) =
    app.MapGet("/places/{slug}", RequestDelegate(guide source)) |> ignore
    app.MapGet("/sitemap.xml", RequestDelegate(sitemap source)) |> ignore
