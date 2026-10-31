module Namastay.Web.Media.MediaRoute

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Namastay.Data.Media
open Namastay.Shared.Contracts
open Namastay.Web.Http

/// Images keep one address everywhere, whichever store sits behind it.
let map (app: IEndpointRouteBuilder) (store: MediaStore) =
    let serve (context: HttpContext) : Task =
        task {
            let key = string context.Request.RouteValues["key"]
            let! found = MediaStore.tryGet store key

            match found with
            | None -> do! Errors.write context (ApiError.notFound "That image doesn't exist.")
            | Some file ->
                use content = file.Content
                context.Response.ContentType <- file.ContentType
                context.Response.ContentLength <- Nullable file.Length
                context.Response.Headers.CacheControl <- "public, max-age=31536000, immutable"
                context.Response.Headers.XContentTypeOptions <- "nosniff"

                context.Response.Headers.ContentSecurityPolicy <-
                    "default-src 'none'; style-src 'unsafe-inline'; sandbox"

                do! content.CopyToAsync context.Response.Body
        }

    app.MapGet("/media/{**key}", RequestDelegate serve) |> ignore
