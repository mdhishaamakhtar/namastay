namespace Namastay.IntegrationTests.Media

open System
open System.IO
open System.Net
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Xunit
open Namastay.Data.Media
open Namastay.IntegrationTests
open Namastay.Web.Media

[<Collection("media")>]
type MediaTests(media: MediaFixture) =
    let store () =
        task {
            let settings = media.Settings("t" + Guid.NewGuid().ToString "N")
            let store = MediaStore.create settings
            do! MediaStore.ensureBucket store
            return store
        }

    let client store =
        task {
            let builder = WebApplication.CreateBuilder()
            builder.WebHost.UseTestServer() |> ignore
            let app = builder.Build()
            MediaRoute.map app store
            do! app.StartAsync()
            return app.GetTestClient()
        }

    let tempFolder (files: (string * string) list) =
        let folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N")

        for name, content in files do
            let path = Path.Combine(folder, name)
            Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
            File.WriteAllText(path, content)

        folder

    [<Fact>]
    member _.``stored file is served with its type and long cache``() =
        task {
            let! store = store ()
            let! _ = MediaStore.sync store (tempFolder [ "photos/a.svg", "<svg/>" ])
            let! http = client store
            let! response = http.GetAsync "/media/photos/a.svg"
            let! body = response.Content.ReadAsStringAsync()
            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
            Assert.Equal("<svg/>", body)
            Assert.Equal("image/svg+xml", response.Content.Headers.ContentType.MediaType)
            Assert.True(response.Headers.CacheControl.Public)
            Assert.Contains("nosniff", response.Headers.GetValues "X-Content-Type-Options")
            Assert.Contains("sandbox", String.Join(" ", response.Headers.GetValues "Content-Security-Policy"))
            Assert.Equal(Nullable(TimeSpan.FromDays 365.0), response.Headers.CacheControl.MaxAge)
        }

    [<Fact>]
    member _.``missing file returns 404``() =
        task {
            let! store = store ()
            let! http = client store
            let! response = http.GetAsync "/media/photos/nothing.webp"
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        }

    [<Fact>]
    member _.``sync uploads only new or changed files``() =
        task {
            let! store = store ()
            let folder = tempFolder [ "a.webp", "one"; "b.webp", "two" ]
            let! first = MediaStore.sync store folder
            let! second = MediaStore.sync store folder
            File.WriteAllText(Path.Combine(folder, "a.webp"), "changed")
            let! third = MediaStore.sync store folder
            Assert.Equal(2, first)
            Assert.Equal(0, second)
            Assert.Equal(1, third)
        }

    [<Fact>]
    member _.``nested keys keep forward slashes``() =
        task {
            let! store = store ()
            let! _ = MediaStore.sync store (tempFolder [ "photos/goa/a.webp", "x" ])
            let! found = MediaStore.tryGet store "photos/goa/a.webp"
            Assert.True(found.IsSome)
            found.Value.Content.Dispose()
        }

    [<Fact>]
    member _.``store outage fails rather than looking like a missing file``() =
        task {
            let store =
                MediaStore.create {
                    Endpoint = "http://127.0.0.1:1"
                    Bucket = "none"
                    AccessKey = "local"
                    SecretKey = "local"
                }

            let! error =
                Assert.ThrowsAnyAsync<exn>(fun () -> MediaStore.tryGet store "a.webp" :> Task)

            Assert.NotNull error
        }
