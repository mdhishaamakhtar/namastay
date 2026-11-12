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

    /// The progress line moves on once a card has been answered, whichever card comes next.
    let waitForCard (page: IPage) (number: int) =
        page.GetByText($"Card {number} of about").WaitForAsync()

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
            do! page.Mouse.MoveAsync(x + distance, y, MouseMoveOptions(Steps = 12))
            do! page.Mouse.UpAsync()
        }

    /// A swipe's speed comes from the clock, so tests set the clock instead of racing it.
    let withClock (page: IPage) (nextReading: string) =
        task {
            let! _ =
                page.EvaluateAsync $"(() => {{ let now = 0; performance.now = () => ({nextReading}); }})()"

            return ()
        }

    let slowClock (page: IPage) = withClock page "now += 10000"
    let stoppedClock (page: IPage) = withClock page "now"

    /// Passes every card, and fails rather than looping if the cards never end.
    let finishCards (page: IPage) =
        task {
            let mutable swipes = 0
            let cardsLeft = "document.querySelector('.card-text h1')?.textContent ?? ''"
            let! first = page.Locator(".card-text h1").CountAsync()
            let mutable left = first > 0

            while left do
                Assert.True(swipes < 30, "the cards never ended")
                swipes <- swipes + 1
                let! current = page.EvaluateAsync<string> cardsLeft
                do! (button page "Not for me").ClickAsync(LocatorClickOptions(Timeout = 5000.0f))
                let! _ = page.WaitForFunctionAsync($"title => ({cardsLeft}) !== title", current)
                let! remaining = page.Locator(".card-text h1").CountAsync()
                left <- remaining > 0
        }

    let buildNow (page: IPage) = button page "Build my trip now"

    let answerQuestions (page: IPage) =
        task {
            for choice in
                [
                    "Oct"
                    "About a week"
                    "Just me"
                    "Some comfort"
                    "Whatever is best"
                    "I eat everything"
                ] do
                do! (button page choice).ClickAsync()
        }

    [<Fact>]
    member _.``a limited trip's note lines up with the logo``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync(browser.BaseUrl + "/journey?label=North%20India")
            do! page.Locator(".journey-scope").WaitForAsync()

            let! brand =
                page.EvaluateAsync<float> "document.querySelector('.journey-top').getBoundingClientRect().left"

            let! note =
                page.EvaluateAsync<float> "document.querySelector('.journey-scope').getBoundingClientRect().left"

            Assert.Equal(brand, note)
        }

    [<Fact>]
    member _.``undo brings back the last card``() =
        task {
            let! page = openJourney ()
            let! first = title page
            do! Assertions.Expect(button page "Undo").ToBeHiddenAsync()
            do! (button page "Not for me").ClickAsync()
            do! waitForCard page 2
            do! (button page "Undo").ClickAsync()
            do! waitForCard page 1
            let! current = title page
            Assert.Equal(first, current)
        }

    [<Fact>]
    member _.``back keeps earlier answers and reaches the card you stopped on``() =
        task {
            let! page = openJourney ()
            do! (button page "Love it").ClickAsync()
            do! waitForCard page 2
            let! waiting = title page
            do! (buildNow page).ClickAsync()
            do! (button page "Oct").ClickAsync()
            do! (button page "Two weeks").ClickAsync()
            do! (button page "Back").ClickAsync()
            do! Assertions.Expect(button page "Two weeks").ToHaveAttributeAsync("aria-pressed", "true")
            do! (button page "Back").ClickAsync()
            do! Assertions.Expect(button page "Oct").ToHaveAttributeAsync("aria-pressed", "true")
            do! (button page "Back").ClickAsync()
            do! waitForCard page 2
            let! current = title page
            Assert.Equal(waiting, current)
        }

    [<Fact>]
    member _.``drag past 110px collects``() =
        task {
            let! page = openJourney ()
            do! drag page 160.0f
            do! waitForCard page 2
        }

    [<Fact>]
    member _.``small drag springs back``() =
        task {
            let! page = openJourney ()
            let! first = title page
            do! slowClock page
            do! drag page 40.0f

            let! _ =
                page.WaitForFunctionAsync "!document.querySelector('.card-label').style.transform"

            let! current = title page
            Assert.Equal(first, current)
        }

    [<Fact>]
    member _.``a quick flick collects however short``() =
        task {
            let! page = openJourney ()
            do! stoppedClock page
            do! drag page 40.0f
            do! waitForCard page 2
        }

    [<Fact>]
    member _.``buttons work without dragging``() =
        task {
            let! page = openJourney ()
            do! (button page "Love it").ClickAsync()
            do! waitForCard page 2
        }

    [<Fact>]
    member _.``keyboard can answer``() =
        task {
            let! page = openJourney ()
            do! page.Keyboard.PressAsync "ArrowRight"
            do! waitForCard page 2
        }

    [<Fact>]
    member _.``each answer brings the next card``() =
        task {
            let! page = openJourney ()
            do! (button page "Love it").ClickAsync()
            do! waitForCard page 2
            do! (button page "Love it").ClickAsync()
            do! waitForCard page 3
        }

    [<Fact>]
    member _.``passing follows a different path``() =
        task {
            let! page = openJourney ()
            do! (button page "Not for me").ClickAsync()
            do! waitForCard page 2
        }

    [<Fact>]
    member _.``double click answers once``() =
        task {
            let! page = openJourney ()
            do! (button page "Love it").DblClickAsync()
            do! waitForCard page 2
        }

    [<Fact>]
    member _.``stage changes colour after a clear lean``() =
        task {
            let! page = openJourney ()

            let stage () =
                page.EvaluateAsync<string> "getComputedStyle(document.querySelector('.journey')).backgroundColor"

            let! first = stage ()

            let! count = page.Locator(".card-text h1").CountAsync()
            Assert.Equal(1, count)

            for number in 2..6 do
                let! current = title page
                let beach = current = "Palm bays" || current = "Surf towns"
                do! (button page (if beach then "Not for me" else "Love it")).ClickAsync()

                if number < 6 then
                    do! waitForCard page number

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
            do! waitForCard page 2
        }

    [<Fact>]
    member _.``finishing the journey builds a trip``() =
        task {
            let! page = openJourney ()
            do! finishCards page
            do! answerQuestions page
            do! waitForAddress page "**/trip/**"
            do! page.GetByRole(AriaRole.Listitem).First.WaitForAsync()
        }

    [<Fact>]
    member _.``a swipe or two is enough to stop and build``() =
        task {
            let! page = openJourney ()
            let! before = (buildNow page).CountAsync()
            Assert.Equal(0, before)
            do! (button page "Not for me").ClickAsync()
            do! waitForCard page 2
            do! (buildNow page).ClickAsync()
            do! answerQuestions page
            do! waitForAddress page "**/trip/**"
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
            do! waitForAddress page "**/trip/**"
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
