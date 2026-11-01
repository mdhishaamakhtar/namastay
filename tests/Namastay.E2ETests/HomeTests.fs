namespace Namastay.E2ETests

open Microsoft.Playwright
open Xunit

type HomeTests(browser: BrowserFixture) =
    let openHome () =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync browser.BaseUrl
            return page
        }

    let search (page: IPage) (text: string) =
        page.GetByRole(AriaRole.Searchbox).FillAsync text

    [<Fact>]
    member _.``plan my trip opens journey``() =
        task {
            let! page = openHome ()
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Plan my trip")).ClickAsync()
            do! page.WaitForURLAsync "**/journey"
        }

    [<Fact>]
    member _.``typing shows results``() =
        task {
            let! page = openHome ()
            do! search page "example"
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Example Valley")).WaitForAsync()
        }

    [<Fact>]
    member _.``result opens guide``() =
        task {
            let! page = openHome ()
            do! search page "example valley"
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Example Valley")).ClickAsync()
            do! page.WaitForURLAsync "**/places/example-valley"
            let! title = page.Locator("h1").First.TextContentAsync()
            Assert.Equal("Example Valley", title)
        }

    [<Fact>]
    member _.``no results shows message``() =
        task {
            let! page = openHome ()
            do! search page "zzzzqqqq"
            do! page.GetByText("We don't have a guide for that yet").WaitForAsync()
        }

    [<Fact>]
    member _.``feeling tile fills search``() =
        task {
            let! page = openHome ()
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Beaches")).ClickAsync()
            let! value = page.GetByRole(AriaRole.Searchbox).InputValueAsync()
            Assert.Equal("Goa", value)
        }

    [<Fact>]
    member _.``unpublished result says guide coming``() =
        task {
            let! page = openHome ()
            do! search page "example island"
            do! page.GetByText("guide coming soon").WaitForAsync()

            let! links =
                page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Example Island")).CountAsync()

            Assert.Equal(0, links)
        }

    [<Fact>]
    member _.``clearing the search removes results``() =
        task {
            let! page = openHome ()
            do! search page "example"
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Example Valley")).WaitForAsync()
            do! search page ""

            let! links =
                page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Example Valley")).CountAsync()

            Assert.Equal(0, links)
        }

    [<Fact>]
    member _.``failed search offers retry``() =
        task {
            let! page = browser.NewPage()

            let! _ =
                page.RouteAsync("**/api/places**", (fun (route: IRoute) -> route.AbortAsync()))

            let! _ = page.GotoAsync browser.BaseUrl
            do! search page "example"
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Try again")).WaitForAsync()
        }

    [<Fact>]
    member _.``page has no sideways scroll on a phone``() =
        task {
            let! page = openHome ()

            let! wide =
                page.EvaluateAsync<bool> "document.documentElement.scrollWidth > window.innerWidth"

            Assert.False wide
        }

    interface IClassFixture<BrowserFixture>
