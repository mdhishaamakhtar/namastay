namespace Namastay.IntegrationTests.Guides

open System
open System.Net
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Npgsql
open Xunit
open Namastay.Data
open Namastay.Data.Import
open Namastay.Domain.Catalog
open Namastay.IntegrationTests
open Namastay.IntegrationTests.Support
open Namastay.Web
open Namastay.Web.Guides

[<Collection("postgres")>]
type GuidePageTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let notice = {
        Slug = "camping"
        Scope = NoticeScope.PlaceScope
        ScopeRef = Some "kasol"
        Kind = "closure"
        Severity = "warn"
        Body = "Camping is banned here."
        ValidFrom = None
        ValidTo = None
        Evidence = Sample.evidence
    }

    let photo = {
        Slug = "river"
        PlaceSlug = "kasol"
        FilePath = "photos/river.webp"
        Alt = "The river at dawn."
        Credit = "A photographer"
        Licence = "CC BY 4.0"
        SourceUrl = "https://example.org/river"
        Sort = 1
    }

    let catalog = {
        Sample.catalog with
            Places = [
                Sample.place
                {
                    Sample.place with
                        Slug = "manali"
                        Name = "Manali"
                        Published = false
                        Experiences = []
                }
            ]
            Links = []
            Notices = [ notice ]
            Photos = [ photo ]
    }

    let start () =
        task {
            let! db = scope.NewDatabase()
            migrated db
            Assert.True((Run.run db Run.Apply "test" catalog).IsOk)
            let builder = WebApplication.CreateBuilder()
            builder.WebHost.UseTestServer() |> ignore
            Server.configureServices builder.Services
            let app = scope.Own(builder.Build())
            Server.useErrorHandling app
            GuideRoutes.map app (scope.Own(NpgsqlDataSource.Create db))
            do! app.StartAsync()
            return app.GetTestClient(), db
        }

    let exec (db: string) (sql: string) =
        task {
            use connection = new NpgsqlConnection(db)
            do! connection.OpenAsync()
            use command = new NpgsqlCommand(sql, connection)
            let! _ = command.ExecuteNonQueryAsync()
            return ()
        }

    let page (http: HttpClient) (path: string) = http.GetStringAsync path

    [<Fact>]
    member _.``guide html has content without javascript``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("<h1", html)
            Assert.Contains("A valley town on the Parvati river.", html)
            Assert.Contains("Kheerganga", html)
            Assert.Contains("Old Kasol", html)
            Assert.DoesNotContain("<script", html)
        }

    [<Fact>]
    member _.``guide has title and description``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("<title>Kasol travel guide | Namastay</title>", html)
            Assert.Contains("name=\"description\"", html)
            Assert.Contains("lang=\"en\"", html)
        }

    [<Fact>]
    member _.``guide has last checked date``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("Last checked 1 October 2026", html)
        }

    [<Fact>]
    member _.``guide uses the place colours on first paint``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("--deep:#2F5D3A", html)
        }

    [<Fact>]
    member _.``conflict fact uses hedged wording``() =
        task {
            let! http, db = start ()
            do! exec db "update experiences set status = 'conflict' where slug = 'kheerganga'"
            let! html = page http "/places/kasol"
            Assert.Contains("Sources differ on this", html)
        }

    [<Fact>]
    member _.``two source fact has no hedge``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.DoesNotContain("Sources differ on this", html)
        }

    [<Fact>]
    member _.``notice renders in warning box``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("class=\"warning\"", html)
            Assert.Contains("Camping is banned here.", html)
        }

    [<Fact>]
    member _.``expandable sections use native details``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("<details", html)
        }

    [<Fact>]
    member _.``photo has alt text and credit``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("src=\"/media/photos/river.webp\"", html)
            Assert.Contains("alt=\"The river at dawn.\"", html)
            Assert.Contains("A photographer", html)
            Assert.Contains("CC BY 4.0", html)
        }

    [<Fact>]
    member _.``guide links back to the site``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("href=\"/\"", html)
            Assert.Contains("href=\"/journey\"", html)
        }

    [<Fact>]
    member _.``html in data is escaped``() =
        task {
            let! http, db = start ()
            do! exec db "update places set summary = '<img src=x onerror=alert(1)>' where slug = 'kasol'"
            let! html = page http "/places/kasol"
            Assert.DoesNotContain("<img src=x", html)
        }

    [<Fact>]
    member _.``unpublished place returns 404``() =
        task {
            let! http, _ = start ()
            let! response = http.GetAsync "/places/manali"
            let! html = response.Content.ReadAsStringAsync()
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
            Assert.Contains("href=\"/\"", html)
        }

    [<Fact>]
    member _.``sitemap lists only published``() =
        task {
            let! http, _ = start ()
            let! xml = page http "/sitemap.xml"
            Assert.Contains("/places/kasol</loc>", xml)
            Assert.DoesNotContain("/places/manali", xml)
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()
