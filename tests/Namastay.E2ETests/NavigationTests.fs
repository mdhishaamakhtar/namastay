namespace Namastay.E2ETests

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

type NavigationTests(browser: BrowserFixture) =
    let heading (page: IPage) =
        page.Locator("h1").First.TextContentAsync()

    [<Fact>]
    member _.``browser back returns to previous page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync browser.BaseUrl
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Plan my trip")).ClickAsync()
            do! page.WaitForURLAsync "**/journey"
            let! _ = page.GoBackAsync()
            do! page.WaitForURLAsync(browser.BaseUrl + "/")

            do!
                page
                    .GetByRole(AriaRole.Heading, PageGetByRoleOptions(Name = "India is a lot. Find yours."))
                    .WaitForAsync()
        }

    [<Fact>]
    member _.``direct address loads correct page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/journey")
            do! page.Locator(".card-label:not(:has(.bone))").WaitForAsync()
            let! title = heading page
            Assert.NotEmpty title
        }

    [<Fact>]
    member _.``unknown address shows a designed page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/no/such/page")
            let! title = heading page
            Assert.Equal("Lost in India?", title)
        }

    [<Fact>]
    member _.``trip json decodes in the browser``() =
        task {
            let! page = browser.NewPage()
            let! token = browser.CreateTrip page
            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.GetByRole(AriaRole.Listitem).First.WaitForAsync()
            let! title = heading page
            Assert.Contains("days in", title)
        }

    [<Fact>]
    member _.``missing trip shows the lost page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/trip/AAAAAAAAAAAA")
            do! page.GetByText("This road doesn't go anywhere").WaitForAsync()
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

    interface IClassFixture<BrowserFixture>
