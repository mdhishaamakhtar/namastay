namespace Namastay.E2ETests

open System
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

/// A real but tiny image. Flows don't depend on photos, and the media tests cover serving them.
module private Photos =
    let private tinyPng =
        Convert.FromBase64String
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="

    let serveTiny (route: IRoute) =
        route.FulfillAsync(RouteFulfillOptions(ContentType = "image/png", BodyBytes = tinyPng))

/// One Chromium per test class, so classes run in parallel. Sized like a phone. Run with `make test-e2e`.
type BrowserFixture() =
    let mutable playwright: IPlaywright = null
    let mutable browser: IBrowser = null

    /// A failing step should fail in seconds, not hold the whole run for half a minute.
    let newPage (context: IBrowserContext) =
        task {
            context.SetDefaultTimeout 8000.0f
            let! _ = context.RouteAsync("**/media/**", Func<IRoute, Task>(Photos.serveTiny))
            return! context.NewPageAsync()
        }

    member _.BaseUrl =
        match Environment.GetEnvironmentVariable "NAMASTAY_URL" with
        | null
        | "" -> "http://localhost:5173"
        | value -> value

    member _.NewReducedMotionPage() =
        task {
            let! context =
                browser.NewContextAsync(BrowserNewContextOptions(ReducedMotion = ReducedMotion.Reduce))

            return! newPage context
        }

    member _.NewPage() =
        task {
            let! context =
                browser.NewContextAsync(
                    BrowserNewContextOptions(ViewportSize = ViewportSize(Width = 390, Height = 844), HasTouch = true)
                )

            return! newPage context
        }

    interface IAsyncLifetime with
        member _.InitializeAsync() =
            task {
                let! started = Playwright.CreateAsync()
                playwright <- started
                let! launched = started.Chromium.LaunchAsync()
                browser <- launched
            }

        member _.DisposeAsync() =
            task {
                do! browser.CloseAsync()
                playwright.Dispose()
            }
