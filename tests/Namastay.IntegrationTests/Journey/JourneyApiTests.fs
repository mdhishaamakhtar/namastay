namespace Namastay.IntegrationTests.Journey

open System
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

type JourneyApiTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let start' = {
        Slug = "start"
        PlaceSlug = Some "kasol"
        Title = "Green valleys?"
        Line = "River noise and pine forests."
        PhotoSlug = None
        IllustrationSlug = Some "valley"
        IsStart = true
        Landing = None
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
            Landing = None
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
                    Status = IllustrationStatus.Draft
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
        Scope = None
    }

    let start () =
        TestApp.start scope catalog (fun app data ->
            JourneyApi.map app data
            TripsApi.map app data)


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
                sql
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
            do! sql db "update places set published = false"
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
            do! sql db "update places set name = 'Renamed'"
            let! loaded = fetch http saved.ShareToken
            Assert.DoesNotContain("Renamed", loaded.Trip.Stops |> List.map _.Name)
        }

    [<Fact>]
    member _.``retired place is marked in shared trip``() =
        task {
            let! http, db = start ()
            let! saved = create http
            let retired = saved.Trip.Stops.Head.PlaceSlug
            do! sql db $"update places set retired_at = now() where slug = '{retired}'"
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

    [<Fact>]
    member _.``visitors behind the host's proxy each get their own limit``() =
        task {
            let! http, _ = start ()
            http.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.1")

            for _ in 1 .. Server.tripsPerMinute do
                let! _ = post http tripRequest
                ()

            http.DefaultRequestHeaders.Remove "X-Forwarded-For" |> ignore
            http.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.2")
            let! response = post http tripRequest
            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        }

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(13)>]
    member _.``month outside 1 to 12 returns 400``(month: int) =
        task {
            let! http, _ = start ()
            let! response = post http { tripRequest with Month = Some month }
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            Assert.Equal("month", error.Fields.Head.Field)
        }

    [<Fact>]
    member _.``every invalid field is reported together``() =
        task {
            let! http, _ = start ()

            let! response =
                post http {
                    tripRequest with
                        Days = 1
                        Month = Some 13
                }

            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal<string list>([ "days"; "month" ], error.Fields |> List.map _.Field)
        }

    [<Fact>]
    member _.``too many answers returns 400``() =
        task {
            let! http, _ = start ()

            let answers = [
                for number in 1..1001 ->
                    {
                        Card = $"card-{number}"
                        Liked = true
                    }
            ]

            let! response = post http { tripRequest with Answers = answers }
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            Assert.Equal("answers", error.Fields.Head.Field)
        }

    [<Theory>]
    [<InlineData("null")>]
    [<InlineData("")>]
    [<InlineData("[]")>]
    [<InlineData("{\"days\":7}")>]
    member _.``unusable json body returns 400``(body: string) =
        task {
            let! http, _ = start ()
            use content = new StringContent(body, Encoding.UTF8, "application/json")
            let! response = http.PostAsync(Routes.createItinerary.Template, content)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            Assert.Equal(ErrorCode.ValidationFailed, error.Code)
        }

    [<Fact>]
    member _.``non json body returns 415 with the error shape``() =
        task {
            let! http, _ = start ()
            use content = new StringContent("days=7", Encoding.UTF8, "text/plain")
            let! response = http.PostAsync(Routes.createItinerary.Template, content)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode)
            Assert.Equal(ErrorCode.UnsupportedMedia, error.Code)
        }

    [<Fact>]
    member _.``oversized body returns 413 with the error shape``() =
        task {
            let! http, _ = start ()

            use content =
                new StringContent(String('a', 200_000), Encoding.UTF8, "application/json")

            let! response = http.PostAsync(Routes.createItinerary.Template, content)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode)
            Assert.Equal(ErrorCode.TooLarge, error.Code)
        }

    [<Theory>]
    [<InlineData("a%20b")>]
    [<InlineData("toolongtokentoolongtokentoolongtoken")>]
    member _.``malformed share token returns 404``(token: string) =
        task {
            let! http, _ = start ()
            let! response = http.GetAsync("/api/itineraries/" + token)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
            Assert.Equal(ErrorCode.NotFound, error.Code)
        }

    [<Fact>]
    member _.``card graph supports conditional requests``() =
        task {
            let! http, _ = start ()
            let! first = http.GetAsync Routes.journeyStart.Template
            Assert.NotNull first.Headers.ETag
            use request = new HttpRequestMessage(HttpMethod.Get, Routes.journeyStart.Template)
            request.Headers.IfNoneMatch.Add first.Headers.ETag
            let! second = http.SendAsync request
            Assert.Equal(HttpStatusCode.NotModified, second.StatusCode)
        }

    [<Fact>]
    member _.``trips reuse the cached catalog until an import``() =
        task {
            let! http, db = start ()
            let! first = create http
            do! sql db "update places set name = 'Renamed'"
            let! second = create http
            Assert.Equal<string list>(first.Trip.Stops |> List.map _.Name, second.Trip.Stops |> List.map _.Name)
        }

    [<Fact>]
    member _.``trip saved before a format change still opens``() =
        task {
            let! http, db = start ()
            let! saved = create http
            do! sql db "update itineraries set trip = trip - 'mix' #- '{stops,0,photo}'"
            let! loaded = fetch http saved.ShareToken
            Assert.Empty loaded.Trip.Mix
            Assert.Equal(saved.Trip.Title, loaded.Trip.Title)
            Assert.True(loaded.Trip.Stops.Head.Photo.IsNone)
        }

    [<Fact>]
    member _.``unreadable saved trip returns a typed error``() =
        task {
            let! http, db = start ()
            let! saved = create http
            do! sql db "update itineraries set trip = '{\"title\":1}'"

            let! response =
                http.GetAsync(Route.url [ "shareToken", saved.ShareToken ] [] Routes.getItinerary)

            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode)
            Assert.Equal(ErrorCode.Conflict, error.Code)
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()

    interface IClassFixture<PostgresFixture>
