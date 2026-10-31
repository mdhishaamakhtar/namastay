module Namastay.Tests.HealthTests

open System.Net
open Microsoft.AspNetCore.Mvc.Testing
open Xunit
open Namastay.Web

[<Fact>]
let health_returns_ok () =
    task {
        use factory = new WebApplicationFactory<Marker>()
        use client = factory.CreateClient()
        let! response = client.GetAsync "/health"
        let! body = response.Content.ReadAsStringAsync()
        Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        Assert.Equal("""{"status":"ok"}""", body)
    }
