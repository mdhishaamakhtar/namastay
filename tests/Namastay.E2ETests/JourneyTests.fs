namespace Namastay.E2ETests

open System
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

type JourneyTests(browser: BrowserFixture) =
    let goToJourney (page: IPage) =
        task {
            let! _ = page.GotoAsync(browser.BaseUrl + "/journey")
            do! page.Locator(".card-label:not(:has(.bone))").WaitForAsync()
        }

    let openJourney () =
        task {
            let! page = browser.NewPage()
            do! goToJourney page
            return page
        }

    let title (page: IPage) =
        page.Locator(".card-text h1").TextContentAsync()

    let waitForTitle (page: IPage) (text: string) =
        page.Locator(".card-text h1", PageLocatorOptions(HasText = text)).WaitForAsync()

    let button (page: IPage) (name: string) =
        page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = name))

    let drag (page: IPage) (distance: float32) =
        task {
            let! box = page.Locator(".card-label").BoundingBoxAsync()
            let x = box.X + box.Width / 2.0f
            let y = box.Y + box.Height / 2.0f
            do! page.Mouse.MoveAsync(x, y)
            do! page.Mouse.DownAsync()
            do! page.WaitForTimeoutAsync(120.0f)
            do! page.Mouse.MoveAsync(x + distance, y, MouseMoveOptions(Steps = 12))
            do! page.WaitForTimeoutAsync(120.0f)
            do! page.Mouse.UpAsync()
        }

    /// Passes every card, and fails rather than looping if the cards never end.
    let finishCards (page: IPage) =
        task {
            let mutable swipes = 0
            let! first = page.Locator(".card-text h1").CountAsync()
            let mutable left = first > 0

            while left do
                Assert.True(swipes < 30, "the cards never ended")
                swipes <- swipes + 1
                do! (button page "Not for me").ClickAsync(LocatorClickOptions(Timeout = 5000.0f))
                do! page.WaitForTimeoutAsync(320.0f)
                let! remaining = page.Locator(".card-text h1").CountAsync()
                left <- remaining > 0
        }

    let answerQuestions (page: IPage) =
        task {
            for choice in [ "Oct"; "About a week"; "Just me"; "Some comfort"; "Whatever is best" ] do
                do! (button page choice).ClickAsync()
        }

    [<Fact>]
    member _.``drag past 110px collects``() =
        task {
            let! page = openJourney ()
            do! drag page 160.0f
            do! waitForTitle page "High trails"
        }

    [<Fact>]
    member _.``small drag springs back``() =
        task {
            let! page = openJourney ()
            do! drag page 40.0f
            do! page.WaitForTimeoutAsync(500.0f)
            let! current = title page
            Assert.Equal("Green valleys", current)
        }

    [<Fact>]
    member _.``buttons work without dragging``() =
        task {
            let! page = openJourney ()
            do! (button page "Love it").ClickAsync()
            do! waitForTitle page "High trails"
        }

    [<Fact>]
    member _.``keyboard can answer``() =
        task {
            let! page = openJourney ()
            do! page.Keyboard.PressAsync "ArrowRight"
            do! waitForTitle page "High trails"
        }

    [<Fact>]
    member _.``branch card appears after collecting``() =
        task {
            let! page = openJourney ()
            do! (button page "Love it").ClickAsync()
            do! waitForTitle page "High trails"
            do! (button page "Love it").ClickAsync()
            do! waitForTitle page "Quiet villages"
        }

    [<Fact>]
    member _.``passing follows a different path``() =
        task {
            let! page = openJourney ()
            do! (button page "Not for me").ClickAsync()
            do! waitForTitle page "Palm bays"
        }

    [<Fact>]
    member _.``double click answers once``() =
        task {
            let! page = openJourney ()
            do! (button page "Love it").DblClickAsync()
            do! waitForTitle page "High trails"
        }

    [<Fact>]
    member _.``stage changes colour after a clear lean``() =
        task {
            let! page = openJourney ()

            let stage () =
                page.EvaluateAsync<string> "getComputedStyle(document.querySelector('.journey')).backgroundColor"

            let! first = stage ()

            for next in [ "High trails"; "Quiet villages" ] do
                do! (button page "Love it").ClickAsync()
                do! waitForTitle page next

            do! (button page "Love it").ClickAsync()

            let! _ =
                page.WaitForFunctionAsync(
                    "before => getComputedStyle(document.querySelector('.journey')).backgroundColor !== before",
                    first
                )

            ()
        }

    [<Fact>]
    member _.``journey continues offline after start``() =
        task {
            let! page = openJourney ()
            do! page.Context.SetOfflineAsync true
            do! (button page "Love it").ClickAsync()
            do! waitForTitle page "High trails"
        }

    [<Fact>]
    member _.``finishing the journey builds a trip``() =
        task {
            let! page = openJourney ()
            do! finishCards page
            do! answerQuestions page
            do! page.WaitForURLAsync "**/trip/**"
            do! page.GetByRole(AriaRole.Listitem).First.WaitForAsync()
        }

    [<Fact>]
    member _.``failed trip request offers retry``() =
        task {
            let! page = openJourney ()
            do! finishCards page
            let mutable block = true

            let! _ =
                page.RouteAsync(
                    "**/api/itineraries",
                    Func<IRoute, Task>(fun route -> if block then route.AbortAsync() else route.ContinueAsync())
                )

            do! answerQuestions page
            do! (button page "Try again").WaitForAsync()
            block <- false
            do! (button page "Try again").ClickAsync()
            do! page.WaitForURLAsync "**/trip/**"
        }

    [<Fact>]
    member _.``failed card load offers retry``() =
        task {
            let! page = browser.NewPage()

            let! _ =
                page.RouteAsync("**/api/journey/start", Func<IRoute, Task>(fun route -> route.AbortAsync()))

            let! _ = page.GotoAsync(browser.BaseUrl + "/journey")
            do! (button page "Try again").WaitForAsync()
        }

    [<Fact>]
    member _.``reduced motion removes movement``() =
        task {
            let! page = browser.NewReducedMotionPage()
            do! goToJourney page
            let! box = page.Locator(".card-label").BoundingBoxAsync()
            let x = box.X + box.Width / 2.0f
            let y = box.Y + box.Height / 2.0f
            do! page.Mouse.MoveAsync(x, y)
            do! page.Mouse.DownAsync()
            do! page.Mouse.MoveAsync(x + 60.0f, y, MouseMoveOptions(Steps = 6))

            let! moved =
                page.EvaluateAsync<string> "document.querySelector('.card-label').style.transform"

            Assert.Equal("", moved)
            do! page.Mouse.UpAsync()
        }

    [<Fact>]
    member _.``no sideways scroll on a phone``() =
        task {
            let! page = openJourney ()

            let! wide =
                page.EvaluateAsync<bool> "document.documentElement.scrollWidth > window.innerWidth"

            Assert.False wide
        }

    interface IClassFixture<BrowserFixture>
