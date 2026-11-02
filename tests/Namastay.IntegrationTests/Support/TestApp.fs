namespace Namastay.IntegrationTests.Support

open System
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Npgsql
open Xunit
open Namastay.Data.Import
open Namastay.Domain.Catalog
open Namastay.IntegrationTests
open Namastay.Web
open Namastay.Web.Auth
open Namastay.Web.Http

[<RequireQualifiedAccess>]
module TestApp =
    let testSite: Tokens.Site = {
        TokenKey = "only-for-tests-and-long-enough-to-sign"
        Url = "https://site.test"
    }

    /// A migrated database holding `catalog`, and a client for an app with the given routes.
    let start (scope: DatabaseScope) (catalog: Catalog) (mapRoutes: WebApplication -> DataCache -> unit) =
        task {
            let! db = scope.NewDatabase()
            migrated db

            match Run.run db Run.Apply "test" catalog with
            | Ok _ -> ()
            | Error errors -> raise (Xunit.Sdk.XunitException $"test data failed to import: {errors}")

            let builder = WebApplication.CreateBuilder()
            builder.WebHost.UseTestServer() |> ignore
            Server.configureServices testSite builder.Services
            let app = scope.Own(builder.Build())
            Server.usePipeline app
            let source = scope.Own(NpgsqlDataSource.Create db)
            mapRoutes app (scope.Own(DataCache.create TimeSpan.Zero source))
            do! app.StartAsync()
            let client: HttpClient = app.GetTestClient()
            return client, db
        }
