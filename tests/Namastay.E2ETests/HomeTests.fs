namespace Namastay.E2ETests

open System
open System.Threading.Tasks
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

            do!
                page
                    .Locator(".results")
                    .GetByRole(AriaRole.Link, LocatorGetByRoleOptions(Name = "Example Valley"))
                    .WaitForAsync()
        }

    [<Fact>]
    member _.``result opens guide``() =
        task {
            let! page = openHome ()
            do! search page "example valley"

            do!
                page
                    .Locator(".results")
                    .GetByRole(AriaRole.Link, LocatorGetByRoleOptions(Name = "Example Valley"))
                    .ClickAsync()

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
    member _.``unpublished result says guide coming``() =
        task {
            let! page = openHome ()
            do! search page "example island"
            do! page.GetByText("guide coming soon").WaitForAsync()

            let! links =
                page
                    .Locator(".results")
                    .GetByRole(AriaRole.Link, LocatorGetByRoleOptions(Name = "Example Island"))
                    .CountAsync()

            Assert.Equal(0, links)
        }

    [<Fact>]
    member _.``a feeling opens its own page``() =
        task {
            let! page = openHome ()
            do! page.Locator(".mood:not(.bone)").First.ClickAsync()
            do! page.Locator(".browse-plan").WaitForAsync()
        }

    [<Fact>]
    member _.``a feeling page plans a trip around it and another feeling``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/feelings/mountains")
            let plan = page.Locator ".browse-plan .button"

            let beaches =
                page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Beaches"))

            do! Assertions.Expect(plan).ToHaveTextAsync "Plan a mountains trip"
            do! beaches.ClickAsync()
            do! Assertions.Expect(beaches).ToHaveAttributeAsync("aria-pressed", "true")
            do! Assertions.Expect(plan).ToHaveTextAsync "Plan a trip around both"

            let! title =
                page.EvaluateAsync<float> "document.querySelector('.browse-title h1').getBoundingClientRect().left"

            let! chip =
                page.EvaluateAsync<float> "document.querySelector('.mix-chip').getBoundingClientRect().left"

            Assert.Equal(title, chip)
            do! plan.ClickAsync()
            do! page.WaitForURLAsync "**/journey?scope=feeling&of=mountains%2Cbeaches**"
            do! Assertions.Expect(page.Locator ".journey-scope").ToHaveTextAsync "Mountains and Beaches only"
            do! page.Locator(".card-label:not(:has(.bone))").WaitForAsync()
        }

    [<Fact>]
    member _.``a feeling taken out again plans from the page's own``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/feelings/mountains")

            let beaches =
                page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Beaches"))

            do! beaches.ClickAsync()
            do! beaches.ClickAsync()
            do! Assertions.Expect(beaches).ToHaveAttributeAsync("aria-pressed", "false")
            do! Assertions.Expect(page.Locator ".browse-plan .button").ToHaveTextAsync "Plan a mountains trip"
        }

    [<Fact>]
    member _.``clearing the search removes results``() =
        task {
            let! page = openHome ()
            do! search page "example"

            do!
                page
                    .Locator(".results")
                    .GetByRole(AriaRole.Link, LocatorGetByRoleOptions(Name = "Example Valley"))
                    .WaitForAsync()

            do! search page ""

            let! links =
                page
                    .Locator(".results")
                    .GetByRole(AriaRole.Link, LocatorGetByRoleOptions(Name = "Example Valley"))
                    .CountAsync()

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

    [<Fact>]
    member _.``a place on the wall opens its guide``() =
        task {
            let! page = openHome ()
            do! page.Locator(".label-link").First.ClickAsync()
            do! page.WaitForURLAsync "**/places/**"
        }

    [<Fact>]
    member _.``the last call opens the journey``() =
        task {
            let! page = openHome ()
            do! page.Locator(".home-again").ClickAsync()
            do! page.WaitForURLAsync "**/journey"
        }

    [<Fact>]
    member _.``places lists regions and a region offers its own trip``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/explore")
            do! page.Locator("a.region-top").First.ClickAsync()
            do! page.WaitForURLAsync "**/explore/**"
            do! page.Locator(".browse-plan a").ClickAsync()
            do! page.WaitForURLAsync "**/journey?scope=region**"
            do! page.Locator(".journey-scope").WaitForAsync()
        }

    [<Fact>]
    member _.``food leads to a region and a dish``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/food")
            do! page.Locator(".region-card li a").First.ClickAsync()
            do! page.WaitForURLAsync "**/dishes/**"
            do! page.Locator("a.listing").First.WaitForAsync()
        }

    [<Fact>]
    member _.``food search finds a dish and says when it can't``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/food")

            let search =
                page.GetByRole(AriaRole.Searchbox, PageGetByRoleOptions(Name = "Find a dish"))

            do! search.FillAsync "soup"
            do! page.Locator(".region-card li a", PageLocatorOptions(HasText = "Example soup")).WaitForAsync()
            do! search.FillAsync "nothing like this"
            do! page.GetByText("We don't have that dish yet.").WaitForAsync()
        }

    [<Fact>]
    member _.``each public page names itself for search``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/food")

            let! _ =
                page.WaitForFunctionAsync "document.title.startsWith('What to eat in India')"

            let! canonical = page.Locator("link[rel=canonical]").GetAttributeAsync "href"
            Assert.EndsWith("/food", canonical)
        }

    [<Fact>]
    member _.``a saved page shows its own data at once and survives a failed refresh``() =
        task {
            let! page = browser.NewPage()

            let! _ =
                page.AddInitScriptAsync(
                    script =
                        """window.__seed = { "/api/food": JSON.stringify({ cards: [], regions: [{ slug: "seeded", name: "Seeded region", colour: "#000000", dishes: [] }] }) }"""
                )

            let! _ =
                page.RouteAsync("**/api/food", Func<IRoute, Task>(fun route -> route.AbortAsync()))

            let! _ = page.GotoAsync(browser.BaseUrl + "/food")
            do! page.GetByText("Seeded region").WaitForAsync()
            let! skeletons = page.Locator(".bone").CountAsync()
            Assert.Equal(0, skeletons)
        }

    [<Fact>]
    member _.``refreshing keeps the scroll position``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/food")
            do! page.Locator(".site-foot").WaitForAsync()

            let! _ =
                page.EvaluateAsync "document.body.style.minHeight = '3000px'; window.scrollTo(0, 400)"

            let! _ = page.ReloadAsync()
            do! page.Locator(".site-foot").WaitForAsync()

            let! _ =
                page.WaitForFunctionAsync
                    "window.scrollY > 0 || document.documentElement.scrollHeight <= window.innerHeight"

            return ()
        }

    [<Fact>]
    member _.``the footer leads to the privacy page``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/food")

            do!
                page
                    .Locator(".site-foot")
                    .GetByRole(AriaRole.Link, LocatorGetByRoleOptions(Name = "Privacy"))
                    .ClickAsync()

            do! page.GetByRole(AriaRole.Heading, PageGetByRoleOptions(Name = "What we keep")).WaitForAsync()
        }

    [<Fact>]
    member _.``an address that doesn't exist says so``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/nowhere/at/all")
            do! page.GetByText("This road doesn't go anywhere").WaitForAsync()
        }

    [<Fact>]
    member _.``sign in stays out of search``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/sign-in")

            let! _ =
                page.WaitForFunctionAsync "document.querySelector('meta[name=robots]')?.content === 'noindex'"

            return ()
        }

    [<Fact>]
    member _.``an unknown region says so``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/explore/nowhere")
            do! page.GetByText("This road doesn't go anywhere").WaitForAsync()
        }

    interface IClassFixture<BrowserFixture>
