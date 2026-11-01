namespace Namastay.IntegrationTests.Auth

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open Xunit
open Namastay.IntegrationTests
open Namastay.IntegrationTests.Support
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web.Auth
open Namastay.Web.Http
open Namastay.Web.Trips

[<Collection("postgres")>]
type SavedTripsTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let request = {
        Answers = []
        Days = 5
        Month = None
        Travellers = TravellerType.Solo
        Budget = BudgetLevel.Mid
        TransportComfort = TransportComfort.Anything
    }

    let start () =
        TestApp.start scope Sample.catalog (fun app data ->
            AuthApi.map app data.Source
            TripsApi.map app data
            SavedTripsApi.map app data.Source)

    /// Signs up and returns the cookie that proves who is asking.
    let signUp (http: HttpClient) (email: string) =
        task {
            let body = {
                Email = email
                Password = "correct horse battery"
            }

            let! response = http.PostAsJsonAsync(Routes.signUp.Template, body, Json.options)
            return (response.Headers.GetValues "Set-Cookie" |> Seq.head).Split(';')[0]
        }

    let as' (cookie: string) (method: HttpMethod) (url: string) =
        let message = new HttpRequestMessage(method, url)
        message.Headers.Add("Cookie", cookie)
        message

    let createTrip (http: HttpClient) =
        task {
            let! response =
                http.PostAsJsonAsync(Routes.createItinerary.Template, request, Json.options)

            let! saved = response.Content.ReadFromJsonAsync<SavedTrip>(Json.options)
            return saved.ShareToken
        }

    let saveUrl (token: string) =
        Route.url [ "shareToken", token ] [] Routes.saveItinerary

    let deleteUrl (id: string) =
        Route.url [ "id", id ] [] Routes.deleteMine

    let save (http: HttpClient) cookie token =
        http.SendAsync(as' cookie HttpMethod.Post (saveUrl token))

    let list (http: HttpClient) cookie =
        task {
            let! response =
                http.SendAsync(as' cookie HttpMethod.Get Routes.myItineraries.Template)

            return! response.Content.ReadFromJsonAsync<SavedTripSummary list>(Json.options)
        }

    [<Fact>]
    member _.``saving requires sign in``() =
        task {
            let! http, _ = start ()
            let! token = createTrip http
            let! response = http.PostAsync(saveUrl token, null)
            let! error = response.Content.ReadFromJsonAsync<ApiError>(Json.options)
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)
            Assert.Equal(ErrorCode.Unauthorized, error.Code)
        }

    [<Fact>]
    member _.``listing requires sign in``() =
        task {
            let! http, _ = start ()
            let! response = http.GetAsync Routes.myItineraries.Template
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)
        }

    [<Fact>]
    member _.``saved trip appears in list``() =
        task {
            let! http, _ = start ()
            let! cookie = signUp http "asha@example.org"
            let! token = createTrip http
            let! _ = save http cookie token
            let! trips = list http cookie
            Assert.Equal(token, trips.Head.ShareToken)
            Assert.Equal(5, trips.Head.Days)
        }

    [<Fact>]
    member _.``saving twice keeps one copy``() =
        task {
            let! http, _ = start ()
            let! cookie = signUp http "asha@example.org"
            let! token = createTrip http
            let! first = save http cookie token
            let! second = save http cookie token
            let! trips = list http cookie
            Assert.Equal(HttpStatusCode.OK, first.StatusCode)
            Assert.Equal(HttpStatusCode.OK, second.StatusCode)
            Assert.Equal(1, trips.Length)
        }

    [<Fact>]
    member _.``saving an unknown trip returns 404``() =
        task {
            let! http, _ = start ()
            let! cookie = signUp http "asha@example.org"
            let! response = save http cookie "AAAAAAAAAAAA"
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        }

    [<Fact>]
    member _.``users cannot see each others trips``() =
        task {
            let! http, _ = start ()
            let! asha = signUp http "asha@example.org"
            let! ravi = signUp http "ravi@example.org"
            let! token = createTrip http
            let! _ = save http asha token
            let! trips = list http ravi
            Assert.Empty trips
        }

    [<Fact>]
    member _.``users cannot delete each others trips``() =
        task {
            let! http, _ = start ()
            let! asha = signUp http "asha@example.org"
            let! ravi = signUp http "ravi@example.org"
            let! token = createTrip http
            let! _ = save http asha token
            let! trips = list http asha
            let! response = http.SendAsync(as' ravi HttpMethod.Delete (deleteUrl trips.Head.Id))
            let! still = list http asha
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
            Assert.Equal(1, still.Length)
        }

    [<Fact>]
    member _.``deleting keeps shared link working``() =
        task {
            let! http, _ = start ()
            let! cookie = signUp http "asha@example.org"
            let! token = createTrip http
            let! _ = save http cookie token
            let! trips = list http cookie
            let! deleted = http.SendAsync(as' cookie HttpMethod.Delete (deleteUrl trips.Head.Id))
            let! after = list http cookie
            let! shared = http.GetAsync(Route.url [ "shareToken", token ] [] Routes.getItinerary)
            Assert.Equal(HttpStatusCode.OK, deleted.StatusCode)
            Assert.Empty after
            Assert.Equal(HttpStatusCode.OK, shared.StatusCode)
        }

    [<Fact>]
    member _.``malformed id returns 404``() =
        task {
            let! http, _ = start ()
            let! cookie = signUp http "asha@example.org"
            let! response = http.SendAsync(as' cookie HttpMethod.Delete (deleteUrl "not-a-guid"))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()
