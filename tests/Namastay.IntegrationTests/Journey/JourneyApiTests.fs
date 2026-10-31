namespace Namastay.IntegrationTests.Journey

open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Text
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
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web
open Namastay.Web.Http
open Namastay.Web.Journey
open Namastay.Web.Trips

[<Collection("postgres")>]
type JourneyApiTests(postgres: PostgresFixture) =
    let start' = {
        Slug = "start"
        PlaceSlug = Some "kasol"
        Title = "Green valleys?"
        Line = "River noise and pine forests."
        PhotoSlug = None
        IllustrationSlug = Some "valley"
        IsStart = true
        Branches = [
            {
                OnAnswer = true
                ToCard = "next"
                Priority = 1
            }
        ]
        Signals = [
            {
                Signal = "interest:mountains"
                Weight = 3
            }
        ]
    }

    let next = {
        start' with
            Slug = "next"
            PlaceSlug = None
            IllustrationSlug = None
            IsStart = false
            Branches = []
            Signals = []
    }

    let catalog = {
        Sample.catalog with
            Cards = [ start'; next ]
            Illustrations = [
                {
                    Slug = "valley"
                    FilePath = "illustrations/valley.svg"
                    Alt = "A green valley."
                    Brief = "Valley."
                    Status = "draft"
                }
            ]
    }

    let tripRequest = {
        Answers = [ { Card = "start"; Liked = true } ]
        Days = 5
        Month = None
        Travellers = TravellerType.Solo
        Budget = BudgetLevel.Mid
        TransportComfort = TransportComfort.Anything
    }

    let start () =
        task {
            let! db = postgres.NewDatabase()
            Assert.True((Migrate.run db = Ok()))
            Assert.True((Run.run db Run.Apply "test" catalog).IsOk)
            let builder = WebApplication.CreateBuilder()
            builder.WebHost.UseTestServer() |> ignore
            Server.configureServices builder.Services
            let app = builder.Build()
            Server.useErrorHandling app
            Server.useRateLimits app
            let source = NpgsqlDataSource.Create db
            JourneyApi.map app source
            TripsApi.map app source
            Server.mapApiFallback app
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

    let post (http: HttpClient) (request: TripRequest) =
        http.PostAsJsonAsync(Routes.createItinerary.Template, request, Json.options)

    let create (http: HttpClient) =
        task {
            let! response = post http tripRequest
            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
            let! saved = response.Content.ReadFromJsonAsync<SavedTrip>(Json.options)
            return saved
        }

    let fetch (http: HttpClient) (token: string) =
        http.GetFromJsonAsync<SavedTrip>(Route.url [ "shareToken", token ] [] Routes.getItinerary, Json.options)

    [<Fact>]
    member _.``start returns whole card graph``() =
        task {
            let! http, _ = start ()

            let! graph =
                http.GetFromJsonAsync<JourneyGraph>(Routes.journeyStart.Template, Json.options)

            let first = graph.Cards |> List.find _.IsStart
            Assert.Equal(2, graph.Cards.Length)
            Assert.Equal("next", first.Branches.Head.ToCard)
            Assert.Equal(Some "/media/illustrations/valley.svg", first.ImageUrl)
            Assert.Equal(Some "#2F5D3A", first.Colours |> Option.map _.Deep)
        }

    [<Fact>]
    member _.``card without a place has no colours``() =
        task {
            let! http, _ = start ()

            let! graph =
                http.GetFromJsonAsync<JourneyGraph>(Routes.journeyStart.Template, Json.options)

            let plain = graph.Cards |> List.find (fun card -> card.Slug = "next")
            Assert.Equal(None, plain.Colours)
            Assert.Equal(None, plain.ImageUrl)
        }

    [<Fact>]
    member _.``card with a photo shows it with credit and keeps the mark``() =
        task {
            let! http, db = start ()

            do!
                exec
                    db
                    "insert into photos (slug, place_slug, file_path, alt, credit, licence, source_url)
                     values ('river', 'kasol', 'photos/river.webp', 'A river.', 'A photographer', 'CC BY 4.0', 'https://example.org/river');
                     update cards set photo_slug = 'river' where slug = 'start'"

            let! graph =
                http.GetFromJsonAsync<JourneyGraph>(Routes.journeyStart.Template, Json.options)

            let first = graph.Cards |> List.find _.IsStart
            Assert.Equal(Some "/media/photos/river.webp", first.ImageUrl)
            Assert.Equal(Some "A river.", first.ImageAlt)
            Assert.Equal(Some "A photographer", first.ImageCredit)
            Assert.Equal(Some "/media/illustrations/valley.svg", first.MarkUrl)
        }

    [<Fact>]
    member _.``post returns share token``() =
        task {
            let! http, _ = start ()
            let! saved = create http
            Assert.Equal(12, saved.ShareToken.Length)
            Assert.Matches("^[A-Za-z0-9_-]+$", saved.ShareToken)
            Assert.NotEmpty saved.Trip.Stops
        }

    [<Fact>]
    member _.``each trip gets its own token``() =
        task {
            let! http, _ = start ()
            let! first = create http
            let! second = create http
            Assert.NotEqual<string>(first.ShareToken, second.ShareToken)
        }

    [<Fact>]
    member _.``get returns same trip``() =
        task {
            let! http, _ = start ()
            let! saved = create http
            let! loaded = fetch http saved.ShareToken
            Assert.Equal(saved.Trip, loaded.Trip)
        }

    [<Theory>]
    [<InlineData(4)>]
    [<InlineData(22)>]
    member _.``invalid days returns 400``(days: int) =
        task {
            let! http, _ = start ()
            let! response = post http { tripRequest with Days = days }
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            Assert.Equal("days", error.Fields.Head.Field)
        }

    [<Fact>]
    member _.``malformed body returns 400``() =
        task {
            let! http, _ = start ()
            use content = new StringContent("{ not json", Encoding.UTF8, "application/json")
            let! response = http.PostAsync(Routes.createItinerary.Template, content)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            Assert.Equal(ErrorCode.ValidationFailed, error.Code)
        }

    [<Fact>]
    member _.``no publishable places returns a typed error``() =
        task {
            let! http, db = start ()
            do! exec db "update places set published = false"
            let! response = post http tripRequest
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode)
            Assert.Equal(ErrorCode.Conflict, error.Code)
        }

    [<Fact>]
    member _.``shared trip survives data change``() =
        task {
            let! http, db = start ()
            let! saved = create http
            do! exec db "update places set name = 'Renamed'"
            let! loaded = fetch http saved.ShareToken
            Assert.DoesNotContain("Renamed", loaded.Trip.Stops |> List.map _.Name)
        }

    [<Fact>]
    member _.``retired place is marked in shared trip``() =
        task {
            let! http, db = start ()
            let! saved = create http
            let retired = saved.Trip.Stops.Head.PlaceSlug
            do! exec db $"update places set retired_at = now() where slug = '{retired}'"
            let! loaded = fetch http saved.ShareToken
            Assert.True loaded.Trip.Stops.Head.Retired
        }

    [<Fact>]
    member _.``current place is not marked retired``() =
        task {
            let! http, _ = start ()
            let! saved = create http
            let! loaded = fetch http saved.ShareToken
            Assert.All(loaded.Trip.Stops, (fun stop -> Assert.False stop.Retired))
        }

    [<Fact>]
    member _.``unknown token returns 404``() =
        task {
            let! http, _ = start ()

            let! response =
                http.GetAsync(Route.url [ "shareToken", "missing" ] [] Routes.getItinerary)

            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
            Assert.Equal(ErrorCode.NotFound, error.Code)
        }

    [<Fact>]
    member _.``too many trips are rate limited with the error shape``() =
        task {
            let! http, _ = start ()

            for _ in 1 .. Server.tripsPerMinute do
                let! response = post http tripRequest
                Assert.Equal(HttpStatusCode.OK, response.StatusCode)

            let! blocked = post http tripRequest
            let! error = blocked.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode)
            Assert.Equal(ErrorCode.RateLimited, error.Code)
        }
