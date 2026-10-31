module Namastay.IntegrationTests.HealthTests

open System.Net
open Microsoft.AspNetCore.Mvc.Testing
open Xunit
open Namastay.Web

[<Fact>]
let ``health returns ok`` () =
    task {
        use factory = new WebApplicationFactory<Marker>()
        use client = factory.CreateClient()
        let! response = client.GetAsync "/health"
        let! body = response.Content.ReadAsStringAsync()
        Assert.Equal(HttpStatusCode.OK, response.StatusCode)
        Assert.Equal("""{"status":"ok"}""", body)
    }
