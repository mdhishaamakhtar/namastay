namespace Namastay.IntegrationTests.Places

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
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
open Namastay.Web.Places

type PlacesApiTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let catalog = {
        Sample.catalog with
            Places = [
                {
                    Sample.place with
                        Slug = "rishikesh"
                        Name = "Rishikesh"
                        Experiences = []
                }
                {
                    Sample.place with
                        Slug = "kasol"
                        Name = "Kasol"
                }
                {
                    Sample.place with
                        Slug = "goa"
                        Name = "Goa"
                        Published = false
                        Experiences = []
                }
            ]
            Links = []
    }

    let start (catalog: Catalog) =
        TestApp.start scope catalog PlacesApi.map


    let search (http: HttpClient) (query: string) =
        http.GetFromJsonAsync<PlaceSummary list>(Route.url [] [ "query", query ] Routes.searchPlaces, Json.options)

    let guide (http: HttpClient) (slug: string) =
        http.GetFromJsonAsync<PlaceGuide>(Route.url [ "slug", slug ] [] Routes.placeGuide, Json.options)

    [<Fact>]
    member _.``search finds partial name``() =
        task {
            let! http, _ = start catalog
            let! results = search http "rish"
            Assert.Equal("rishikesh", results.Head.Slug)
        }

    [<Fact>]
    member _.``search tolerates misspelling``() =
        task {
            let! http, _ = start catalog
            let! results = search http "Rishkesh"
            Assert.Equal("rishikesh", results.Head.Slug)
        }

    [<Fact>]
    member _.``search by region``() =
        task {
            let! http, _ = start catalog
            let! results = search http "himalaya"
            Assert.Equal(3, results.Length)
        }

    [<Fact>]
    member _.``unknown query returns empty``() =
        task {
            let! http, _ = start catalog
            let! results = search http "zzzzqqqq"
            Assert.Empty results
        }

    [<Fact>]
    member _.``blank query returns empty``() =
        task {
            let! http, _ = start catalog
            let! results = search http "   "
            Assert.Empty results
        }

    [<Fact>]
    member _.``percent sign does not match everything``() =
        task {
            let! http, _ = start catalog
            let! results = search http "%"
            Assert.Empty results
        }

    [<Fact>]
    member _.``very long query is a validation error``() =
        task {
            let! http, _ = start catalog

            let! response =
                http.GetAsync(Route.url [] [ "query", String('a', 101) ] Routes.searchPlaces)

            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            Assert.Equal(ErrorCode.ValidationFailed, error.Code)
        }

    [<Fact>]
    member _.``search is capped at 8 results``() =
        task {
            let many = [
                for number in 1..12 do
                    {
                        Sample.place with
                            Slug = $"hill-{number}"
                            Name = $"Hill {number}"
                            Experiences = []
                    }
            ]

            let! http, _ = start { catalog with Places = many }
            let! results = search http "hill"
            Assert.Equal(8, results.Length)
        }

    [<Fact>]
    member _.``retired place is not found by search``() =
        task {
            let! http, db = start catalog
            do! sql db "update places set retired_at = now() where slug = 'kasol'"
            let! results = search http "kasol"
            Assert.Empty results
        }

    [<Fact>]
    member _.``unpublished place appears in search marked unpublished``() =
        task {
            let! http, _ = start catalog
            let! results = search http "goa"
            Assert.False results.Head.Published
        }

    [<Fact>]
    member _.``published guide carries colours and content``() =
        task {
            let! http, _ = start catalog
            let! place = guide http "kasol"
            Assert.Equal("#2F5D3A", place.Colours.Deep)
            Assert.Equal("Himalaya", place.Region)
            Assert.Equal("Kheerganga", place.Experiences.Head.Name)
            Assert.Equal(MonthRating.Best, place.Months.Head.Rating)
            Assert.Equal("2026-10-01", place.LastChecked)
        }

    [<Fact>]
    member _.``unpublished guide returns 404``() =
        task {
            let! http, _ = start catalog
            let! response = http.GetAsync(Route.url [ "slug", "goa" ] [] Routes.placeGuide)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
            Assert.Equal(ErrorCode.NotFound, error.Code)
        }

    [<Fact>]
    member _.``unknown guide returns 404``() =
        task {
            let! http, _ = start catalog
            let! response = http.GetAsync(Route.url [ "slug", "atlantis" ] [] Routes.placeGuide)
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        }

    [<Fact>]
    member _.``retired guide returns 404``() =
        task {
            let! http, db = start catalog
            do! sql db "update places set retired_at = now() where slug = 'kasol'"
            let! response = http.GetAsync(Route.url [ "slug", "kasol" ] [] Routes.placeGuide)
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        }

    [<Fact>]
    member _.``unverified facts are excluded from guide``() =
        task {
            let! http, db = start catalog
            do! sql db "update experiences set status = 'unverified' where slug = 'kheerganga'"
            do! sql db "update stay_areas set status = 'unverified' where place_slug = 'kasol'"
            do! sql db "update day_plans set status = 'unverified' where place_slug = 'kasol'"
            let! place = guide http "kasol"
            Assert.Empty place.Experiences
            Assert.Empty place.StayAreas
            Assert.Empty place.DayPlans
        }

    [<Fact>]
    member _.``unverified month shows as unknown``() =
        task {
            let! http, db = start catalog
            do! sql db "update place_months set status = 'unverified' where place_slug = 'kasol'"
            let! place = guide http "kasol"
            Assert.Equal(MonthRating.Unknown, place.Months.Head.Rating)
        }

    [<Fact>]
    member _.``conflict facts are flagged for hedged copy``() =
        task {
            let! http, db = start catalog
            do! sql db "update experiences set status = 'conflict' where slug = 'kheerganga'"
            let! place = guide http "kasol"
            Assert.True place.Experiences.Head.Hedged
        }

    [<Fact>]
    member _.``one source facts are flagged for hedged copy``() =
        task {
            let! http, db = start catalog
            do! sql db "update experiences set status = 'one_source' where slug = 'kheerganga'"
            let! place = guide http "kasol"
            Assert.True place.Experiences.Head.Hedged
        }

    [<Fact>]
    member _.``two source facts are not hedged``() =
        task {
            let! http, _ = start catalog
            let! place = guide http "kasol"
            Assert.False place.Experiences.Head.Hedged
        }

    [<Fact>]
    member _.``retired experience is excluded``() =
        task {
            let! http, db = start catalog
            do! sql db "update experiences set retired_at = now() where slug = 'kheerganga'"
            let! place = guide http "kasol"
            Assert.Empty place.Experiences
        }

    [<Fact>]
    member _.``place state and national notices all appear``() =
        task {
            let notice = {
                Slug = "n1"
                Scope = NoticeScope.PlaceScope
                ScopeRef = Some "kasol"
                Kind = NoticeKind.Permit
                Severity = Severity.Warn
                Body = "Place notice."
                ValidFrom = None
                ValidTo = None
                Evidence = Sample.evidence
            }

            let notices = [
                notice
                {
                    notice with
                        Slug = "n2"
                        Scope = NoticeScope.State
                        ScopeRef = Some "Himachal Pradesh"
                        Body = "State notice."
                }
                {
                    notice with
                        Slug = "n3"
                        Scope = NoticeScope.State
                        ScopeRef = Some "Goa"
                        Body = "Other state."
                }
                {
                    notice with
                        Slug = "n4"
                        Scope = NoticeScope.National
                        ScopeRef = None
                        Body = "National notice."
                }
            ]

            let! http, _ = start { catalog with Notices = notices }
            let! place = guide http "kasol"
            let bodies = place.Notices |> List.map _.Body
            Assert.Equal(3, bodies.Length)
            Assert.DoesNotContain("Other state.", bodies)
        }

    [<Fact>]
    member _.``expired notice is hidden``() =
        task {
            let notice = {
                Slug = "old"
                Scope = NoticeScope.PlaceScope
                ScopeRef = Some "kasol"
                Kind = NoticeKind.Closure
                Severity = Severity.Warn
                Body = "Old."
                ValidFrom = Some(DateOnly(2020, 1, 1))
                ValidTo = Some(DateOnly(2020, 2, 1))
                Evidence = Sample.evidence
            }

            let! http, _ = start { catalog with Notices = [ notice ] }
            let! place = guide http "kasol"
            Assert.Empty place.Notices
        }

    [<Fact>]
    member _.``photos use the media address``() =
        task {
            let photo = {
                Slug = "p1"
                PlaceSlug = "kasol"
                FilePath = "photos/kasol.webp"
                Alt = "A river."
                Credit = "Someone"
                Licence = "CC BY 4.0"
                SourceUrl = "https://example.org/photo"
                Sort = 1
            }

            let! http, _ = start { catalog with Photos = [ photo ] }
            let! place = guide http "kasol"
            Assert.Equal("/media/photos/kasol.webp", place.Photos.Head.Url)
            Assert.Equal("Someone", place.Photos.Head.Credit)
        }

    [<Fact>]
    member _.``the landing page gets its chosen cards in order``() =
        task {
            let photo = {
                Slug = "p1"
                PlaceSlug = "kasol"
                FilePath = "photos/kasol.webp"
                Alt = "A river."
                Credit = "Someone"
                Licence = "CC BY 4.0"
                SourceUrl = "https://example.org/photo"
                Sort = 1
            }

            let card slug landing signals = {
                Slug = slug
                PlaceSlug = Some "kasol"
                Title = slug
                Line = "A line."
                PhotoSlug = Some "p1"
                IllustrationSlug = None
                IsStart = false
                Landing = landing
                Branches = []
                Signals = signals
            }

            let! http, _ =
                start {
                    catalog with
                        Photos = [ photo ]
                        Cards = [
                            card "second" (Some 2) [ { Signal = "interest:food"; Weight = 3 } ]
                            card "first" (Some 1) []
                            card "left-out" None []
                        ]
                }

            let! page = http.GetFromJsonAsync<LandingPage>(Routes.landing.Template, Json.options)
            Assert.Equal<string list>([ "first"; "second" ], page.Cards |> List.map _.Title)
            Assert.Equal<bool list>([ false; true ], page.Cards |> List.map _.Food)
            Assert.Equal("/media/photos/kasol.webp", page.Cards.Head.Photo.Url)
            Assert.Equal(catalog.Places |> List.filter _.Published |> List.length, page.PlaceCount)
        }

    [<Fact>]
    member _.``control characters in a search are a validation error``() =
        task {
            let! http, _ = start catalog
            let! response = http.GetAsync "/api/places?query=a%00b"
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            Assert.Equal("query", error.Fields.Head.Field)
        }

    [<Theory>]
    [<InlineData("a%20b")>]
    [<InlineData("UPPER")>]
    [<InlineData("..%2F..")>]
    member _.``malformed guide slug returns 404``(slug: string) =
        task {
            let! http, _ = start catalog
            let! response = http.GetAsync("/api/places/" + slug)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
            Assert.Equal(ErrorCode.NotFound, error.Code)
        }

    [<Fact>]
    member _.``feelings come from the data and open their places``() =
        task {
            let! http, _ = start catalog

            let! feelings =
                http.GetFromJsonAsync<Feeling list>(Routes.feelings.Template, Json.options)

            let first = feelings.Head
            let url = Route.url [ "slug", first.Slug ] [] Routes.feelingPlaces
            let! places = http.GetFromJsonAsync<PlaceSummary list>(url, Json.options)
            Assert.NotEmpty first.Places
            Assert.Contains(places, (fun place -> place.Name = first.Places.Head))
        }

    [<Fact>]
    member _.``an unknown feeling has no places``() =
        task {
            let! http, _ = start catalog
            let url = Route.url [ "slug", "not-a-feeling" ] [] Routes.feelingPlaces
            let! places = http.GetFromJsonAsync<PlaceSummary list>(url, Json.options)
            Assert.Empty places
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()

    interface IClassFixture<PostgresFixture>
