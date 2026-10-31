namespace Namastay.IntegrationTests.Web

open System
open System.Net
open System.Net.Http.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost
open Npgsql
open Xunit
open Namastay.Data
open Namastay.Data.Import
open Namastay.Data.Media
open Namastay.IntegrationTests
open Namastay.IntegrationTests.Support
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web
open Namastay.Web.Http

/// Runs the real Program.fs, so a route that was never wired up fails here.
[<Collection("postgres")>]
type WiringTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let start () =
        task {
            let! db = scope.NewDatabase()
            migrated db
            Assert.True((Run.run db Run.Apply "test" Sample.catalog).IsOk)

            let factory =
                (new WebApplicationFactory<Marker>())
                    .WithWebHostBuilder(fun host ->
                        host.UseSetting("ConnectionStrings:Namastay", db) |> ignore
                        host.UseSetting("Media:Endpoint", "http://127.0.0.1:1") |> ignore)

            return scope.Own(factory).CreateClient()
        }

    [<Fact>]
    member _.``health is wired``() =
        task {
            let! http = start ()

            let! health =
                http.GetFromJsonAsync<HealthStatus>(Routes.health.Template, Json.options)

            Assert.Equal("ok", health.Status)
        }

    [<Fact>]
    member _.``ready confirms the database``() =
        task {
            let! http = start ()

            let! status =
                http.GetFromJsonAsync<HealthStatus>(Routes.ready.Template, Json.options)

            Assert.Equal("ready", status.Status)
        }

    [<Fact>]
    member _.``ready returns 503 with the error shape when the database is down``() =
        task {
            let builder = WebApplication.CreateBuilder()
            builder.WebHost.UseTestServer() |> ignore
            let app = scope.Own(builder.Build())

            let source =
                scope.Own(NpgsqlDataSource.Create "Host=127.0.0.1;Port=1;Database=none;Username=x;Password=x")

            Server.useErrorHandling app
            Server.mapHealth app source
            do! app.StartAsync()
            let http = app.GetTestClient()
            let! response = http.GetAsync Routes.ready.Template
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode)
            Assert.Equal(ErrorCode.Unavailable, error.Code)
        }

    [<Fact>]
    member _.``places api is wired``() =
        task {
            let! http = start ()

            let! results =
                http.GetFromJsonAsync<PlaceSummary list>("/api/places?query=kasol", Json.options)

            Assert.NotEmpty results
        }

    [<Fact>]
    member _.``journey and trips are wired``() =
        task {
            let! http = start ()

            let! graph =
                http.GetFromJsonAsync<JourneyGraph>(Routes.journeyStart.Template, Json.options)

            Assert.Empty graph.Cards

            let request = {
                Answers = []
                Days = 5
                Month = None
                Travellers = TravellerType.Solo
                Budget = BudgetLevel.Mid
                TransportComfort = TransportComfort.Anything
            }

            let! response =
                http.PostAsJsonAsync(Routes.createItinerary.Template, request, Json.options)

            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        }

    [<Fact>]
    member _.``guides and sitemap are wired``() =
        task {
            let! http = start ()
            let! guide = http.GetAsync "/places/kasol"
            let! sitemap = http.GetAsync "/sitemap.xml"
            Assert.Equal(HttpStatusCode.OK, guide.StatusCode)
            Assert.Equal(HttpStatusCode.OK, sitemap.StatusCode)
        }

    [<Fact>]
    member _.``guide fonts are served``() =
        task {
            let! http = start ()
            let! html = http.GetStringAsync "/places/kasol"
            let! yatra = http.GetAsync "/fonts/yatra-one-latin-400-normal.woff2"
            let! mukta = http.GetAsync "/fonts/mukta-latin-400-normal.woff2"
            Assert.Contains("yatra-one-latin-400-normal.woff2", html)
            Assert.Equal(HttpStatusCode.OK, yatra.StatusCode)
            Assert.Equal(HttpStatusCode.OK, mukta.StatusCode)
        }

    [<Fact>]
    member _.``media is wired``() =
        task {
            let! http = start ()
            let! response = http.GetAsync "/media/photos/a.webp"
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode)
        }

    [<Fact>]
    member _.``unknown api address uses the error shape``() =
        task {
            let! http = start ()
            let! response = http.GetAsync "/api/nothing-here"
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(ErrorCode.NotFound, error.Code)
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()
