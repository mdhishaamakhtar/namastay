namespace Namastay.E2ETests

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

[<Collection("browser")>]
type NavigationTests(browser: BrowserFixture) =
    let heading (page: IPage) =
        page.Locator("h1").First.TextContentAsync()

    let createTrip (page: IPage) =
        task {
            let body =
                """{"answers":[],"days":5,"month":null,"travellers":"Solo","budget":"Mid","transportComfort":"Anything"}"""

            let! response =
                page.APIRequest.PostAsync(
                    $"{browser.BaseUrl}/api/itineraries",
                    APIRequestContextOptions(Data = body, Headers = dict [ "Content-Type", "application/json" ])
                )

            Assert.True(response.Ok, "the trip request failed")
            let! text = response.TextAsync()
            use json = JsonDocument.Parse text
            return json.RootElement.GetProperty("shareToken").GetString()
        }

    [<Fact>]
    member _.``browser back returns to previous page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync browser.BaseUrl
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Plan my trip")).ClickAsync()
            do! page.WaitForURLAsync "**/journey"
            let! _ = page.GoBackAsync()
            do! page.WaitForURLAsync(browser.BaseUrl + "/")
            let! title = heading page
            Assert.Equal("India is a lot. Find yours.", title)
        }

    [<Fact>]
    member _.``direct address loads correct page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/journey")
            do! page.Locator(".card-label").WaitForAsync()
            let! title = heading page
            Assert.Equal("Green valleys", title)
        }

    [<Fact>]
    member _.``unknown address shows a designed page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/no/such/page")
            let! title = heading page
            Assert.Equal("We can't find that page", title)
        }

    [<Fact>]
    member _.``trip json decodes in the browser``() =
        task {
            let! page = browser.NewPage()
            let! token = createTrip page
            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.GetByRole(AriaRole.Listitem).First.WaitForAsync()
            let! title = heading page
            Assert.Contains("5 days in", title)
        }

    [<Fact>]
    member _.``missing trip shows the server message``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/trip/AAAAAAAAAAAA")
            let! message = page.GetByRole(AriaRole.Alert).TextContentAsync()
            Assert.Contains("couldn't find that trip", message)
        }

    [<Fact>]
    member _.``failed request offers retry``() =
        task {
            let! page = browser.NewPage()

            let! _ =
                page.RouteAsync("**/api/itineraries/**", Func<IRoute, Task>(fun route -> route.AbortAsync()))

            let! _ = page.GotoAsync(browser.BaseUrl + "/trip/AAAAAAAAAAAA")
            let! message = page.GetByRole(AriaRole.Alert).TextContentAsync()
            Assert.Contains("can't reach Namastay", message)
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Try again")).WaitForAsync()
        }
