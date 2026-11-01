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
open Namastay.Web.Http

type GuidePageTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let notice = {
        Slug = "camping"
        Scope = NoticeScope.PlaceScope
        ScopeRef = Some "kasol"
        Kind = NoticeKind.Closure
        Severity = Severity.Warn
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
        TestApp.start scope catalog GuideRoutes.map


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
            do! sql db "update experiences set status = 'conflict' where slug = 'kheerganga'"
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
    member _.``a guide says what to eat and where``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("What to eat", html)
            Assert.Contains("River trout", html)
            Assert.Contains("The Bridge Cafe", html)
        }

    [<Fact>]
    member _.``each photo opens full size``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("href=\"#photo-1\"", html)
            Assert.Contains("id=\"photo-1\"", html)
        }

    [<Fact>]
    member _.``notice renders in worth knowing``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("class=\"notices\"", html)
            Assert.Contains("Camping is banned here.", html)
        }

    [<Fact>]
    member _.``things to do are all visible``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("class=\"spot\"", html)
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
            do! sql db "update places set summary = '<img src=x onerror=alert(1)>' where slug = 'kasol'"
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

    [<Fact>]
    member _.``repeat guide reads come from the cache``() =
        task {
            let! http, db = start ()
            let! _ = page http "/places/kasol"
            do! sql db "update places set summary = 'Changed behind the cache.' where slug = 'kasol'"
            let! html = page http "/places/kasol"
            Assert.DoesNotContain("Changed behind the cache.", html)
        }

    [<Fact>]
    member _.``a new import refreshes the cache``() =
        task {
            let! http, db = start ()
            let! _ = page http "/places/kasol"
            do! sql db "update places set summary = 'Fresh from an import.' where slug = 'kasol'"

            do!
                sql
                    db
                    "insert into import_batches (started_at, finished_at, file_hash, status)
                     values (now(), now(), 'new', 'committed')"

            let! html = page http "/places/kasol"
            Assert.Contains("Fresh from an import.", html)
        }

    [<Fact>]
    member _.``unknown slugs are not cached``() =
        task {
            let! http, db = start ()
            let! first = http.GetAsync "/places/manali"
            Assert.Equal(HttpStatusCode.NotFound, first.StatusCode)
            do! sql db "update places set published = true where slug = 'manali'"

            do!
                sql
                    db
                    "insert into import_batches (started_at, finished_at, file_hash, status)
                     values (now(), now(), 'new', 'committed')"

            let! second = http.GetAsync "/places/manali"
            Assert.Equal(HttpStatusCode.OK, second.StatusCode)
        }

    [<Fact>]
    member _.``guide sends etag and a matching request gets 304``() =
        task {
            let! http, _ = start ()
            let! first = http.GetAsync "/places/kasol"
            let etag = first.Headers.ETag
            Assert.NotNull etag
            Assert.True first.Headers.CacheControl.Public
            use request = new HttpRequestMessage(HttpMethod.Get, "/places/kasol")
            request.Headers.IfNoneMatch.Add etag
            let! second = http.SendAsync request
            let! body = second.Content.ReadAsStringAsync()
            Assert.Equal(HttpStatusCode.NotModified, second.StatusCode)
            Assert.Empty body
        }

    [<Fact>]
    member _.``guide is compressed when the browser accepts it``() =
        task {
            let! http, _ = start ()
            use request = new HttpRequestMessage(HttpMethod.Get, "/places/kasol")
            request.Headers.AcceptEncoding.ParseAdd "br"
            let! response = http.SendAsync request
            Assert.Contains("br", response.Content.Headers.ContentEncoding)
        }

    [<Fact>]
    member _.``hero is the first photo in a framed label``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("class=\"label\"", html)
            Assert.Contains("class=\"hero\"", html)
        }

    [<Fact>]
    member _.``later photos form the gallery``() =
        task {
            let! http, db = start ()

            do!
                sql
                    db
                    "insert into photos (slug, place_slug, file_path, alt, credit, licence, source_url, sort)
                     values ('second', 'kasol', 'photos/second.webp', 'A bridge.', 'Someone', 'CC0', 'https://example.org/2', 2)"

            let! html = page http "/places/kasol"
            Assert.Contains("class=\"gallery\"", html)
            Assert.Contains("/media/photos/second.webp", html)
        }

    [<Fact>]
    member _.``guide with no photos still renders``() =
        task {
            let! http, db = start ()
            do! sql db "delete from photos"
            let! html = page http "/places/kasol"
            Assert.Contains("<h1", html)
            Assert.DoesNotContain("class=\"hero\"", html)
            Assert.DoesNotContain("class=\"gallery\"", html)
        }

    [<Fact>]
    member _.``illustrated mark appears when one exists``() =
        task {
            let! http, db = start ()

            do!
                sql
                    db
                    "insert into illustrations (slug, file_path, alt, brief, status)
                     values ('kasol-mark', 'marks/kasol.svg', 'A pine.', 'Pine.', 'final')"

            let! html = page http "/places/kasol"
            Assert.Contains("class=\"mark\"", html)
            Assert.Contains("/media/marks/kasol.svg", html)
        }

    [<Fact>]
    member _.``getting there appears when written``() =
        task {
            let! http, db = start ()
            do! sql db "update places set getting_there = 'Fly to Bhuntar.' where slug = 'kasol'"
            let! html = page http "/places/kasol"
            Assert.Contains("Getting there", html)
            Assert.Contains("Fly to Bhuntar.", html)
        }

    [<Fact>]
    member _.``back link appears only with a valid trip``() =
        task {
            let! http, _ = start ()
            let! plain = page http "/places/kasol"
            let! fromTrip = page http "/places/kasol?trip=ABCDEFGHIJKL"
            let! forged = page http "/places/kasol?trip=%22%3E%3Cscript%3E"
            Assert.DoesNotContain("Your trip", plain)
            Assert.Contains("href=\"/trip/ABCDEFGHIJKL\"", fromTrip)
            Assert.DoesNotContain("Your trip", forged)
        }

    [<Fact>]
    member _.``months use three letters with a full name for readers``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("aria-label=\"May: best\"", html)
            Assert.Contains(">May</li>", html)
        }

    [<Fact>]
    member _.``desktop layout is in the page``() =
        task {
            let! http, _ = start ()
            let! html = page http "/places/kasol"
            Assert.Contains("@media (min-width:1024px)", html)
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()

    interface IClassFixture<PostgresFixture>
