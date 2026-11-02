module Namastay.IntegrationTests.Http.ErrorHandlingTests

open System
open System.Net
open System.Net.Http
open System.Text.Json
open System.IO
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.Routing
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Logging.Abstractions
open Xunit
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.Web
open Namastay.Web.Http

let private okRoute: Route<NoBody, PlaceSummary> = Route.get "/api/test/ok"

let private missingRoute: Route<NoBody, PlaceSummary> =
    Route.get "/api/test/missing"

let private invalidRoute: Route<NoBody, PlaceSummary> =
    Route.get "/api/test/invalid"

let private crashRoute: Route<NoBody, PlaceSummary> = Route.get "/api/test/crash"

let private sample = {
    Slug = "goa"
    Name = "Goa"
    Region = "West coast"
    FamilyDeep = "#17427A"
    Published = true
}

let private client () =
    task {
        let builder = WebApplication.CreateBuilder()
        builder.WebHost.UseTestServer() |> ignore

        Server.configureServices
            {
                TokenKey = "only-for-tests-and-long-enough-to-sign"
                Url = "https://site.test"
            }
            builder.Services

        let app = builder.Build()
        Server.useErrorHandling app
        Endpoint.mapGet app okRoute (fun _ -> Task.FromResult(Ok sample))
        Endpoint.mapGet app missingRoute (fun _ -> Task.FromResult(Error(ApiError.notFound "No such place.")))

        Endpoint.mapGet app invalidRoute (fun _ ->
            Task.FromResult(
                Error(
                    ApiError.validation [
                        {
                            Field = "days"
                            Message = "Too long."
                        }
                    ]
                )
            ))

        Endpoint.mapGet app crashRoute (fun _ -> failwith "secret database detail")
        do! app.StartAsync()
        return app.GetTestClient()
    }

let private readJson (response: HttpResponseMessage) =
    task {
        let! text = response.Content.ReadAsStringAsync()
        return text, JsonDocument.Parse(text).RootElement
    }

[<Fact>]
let ``success returns the typed body in camelCase`` () =
    task {
        let! http = client ()
        let! response = http.GetAsync okRoute.Template
        let! _, json = readJson response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        Assert.Equal("#17427A", json.GetProperty("familyDeep").GetString())
    }

[<Fact>]
let ``not found error returns 404 with the error shape`` () =
    task {
        let! http = client ()
        let! response = http.GetAsync missingRoute.Template
        let! _, json = readJson response
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        Assert.Equal("NotFound", json.GetProperty("code").GetString())
        Assert.Equal("No such place.", json.GetProperty("message").GetString())
        Assert.False(String.IsNullOrEmpty(json.GetProperty("traceId").GetString()))
    }

[<Fact>]
let ``validation error returns 400 with its fields`` () =
    task {
        let! http = client ()
        let! response = http.GetAsync invalidRoute.Template
        let! _, json = readJson response
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
        Assert.Equal("days", (json.GetProperty "fields").[0].GetProperty("field").GetString())
    }

[<Fact>]
let ``crash returns a safe internal error`` () =
    task {
        let! http = client ()
        let! response = http.GetAsync crashRoute.Template
        let! text, json = readJson response
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode)
        Assert.Equal("Internal", json.GetProperty("code").GetString())
        Assert.DoesNotContain("secret", text)
        Assert.False(String.IsNullOrEmpty(json.GetProperty("traceId").GetString()))
    }

[<Fact>]
let ``wrong method returns 405 with the error shape`` () =
    task {
        let! http = client ()
        let! response = http.PostAsync(okRoute.Template, null)
        let! _, json = readJson response
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode)
        Assert.Equal("MethodNotAllowed", json.GetProperty("code").GetString())
    }

[<Fact>]
let ``cancelled request is not reported as a server error`` () =
    task {
        use cancelled = new CancellationTokenSource()
        cancelled.Cancel()
        let context = DefaultHttpContext(RequestAborted = cancelled.Token)

        let next =
            RequestDelegate(fun _ -> raise (OperationCanceledException cancelled.Token))

        do! Server.guard NullLogger.Instance context next
        Assert.Equal(499, context.Response.StatusCode)
    }

[<Fact>]
let ``cancellation from elsewhere is still a server error`` () =
    task {
        let context = DefaultHttpContext()
        context.Response.Body <- new MemoryStream()
        let next = RequestDelegate(fun _ -> raise (OperationCanceledException()))
        do! Server.guard NullLogger.Instance context next
        Assert.Equal(500, context.Response.StatusCode)
    }

[<Fact>]
let ``unknown api route returns the error shape`` () =
    task {
        let! http = client ()
        let! response = http.GetAsync "/api/nothing-here"
        let! _, json = readJson response
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        Assert.Equal("NotFound", json.GetProperty("code").GetString())
    }

[<Fact>]
let ``signed-in route turns a visitor away before its handler runs`` () =
    task {
        let route: Route<NoBody, PlaceSummary> = Route.get "/api/test/signed-in"
        let ran = ref false
        let builder = WebApplication.CreateBuilder()
        builder.WebHost.UseTestServer() |> ignore

        Server.configureServices
            {
                TokenKey = "only-for-tests-and-long-enough-to-sign"
                Url = "https://site.test"
            }
            builder.Services

        let app = builder.Build()
        Server.useErrorHandling app
        Server.useSessions app

        Endpoint.mapGet (Endpoint.signedIn app) route (fun _ ->
            ran.Value <- true
            Task.FromResult(Ok sample))

        do! app.StartAsync()
        let http = app.GetTestClient()
        let! response = http.GetAsync route.Template
        let! _, json = readJson response
        Assert.False ran.Value
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)
        Assert.Equal("Unauthorized", json.GetProperty("code").GetString())
    }

[<Fact>]
let ``real app returns the error shape for unknown api routes`` () =
    task {
        use factory = new WebApplicationFactory<Marker>()
        use http = factory.CreateClient()
        let! response = http.GetAsync "/api/nothing-here"
        let! _, json = readJson response
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        Assert.Equal("NotFound", json.GetProperty("code").GetString())
    }
