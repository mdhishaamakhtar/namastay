namespace Namastay.E2ETests

open System
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

/// One Chromium per test class, so classes run in parallel. Sized like a phone. Run with `make test-e2e`.
type BrowserFixture() =
    let mutable playwright: IPlaywright = null
    let mutable browser: IBrowser = null

    /// A failing step should fail in seconds, not hold the whole run for half a minute.
    let newPage (context: IBrowserContext) =
        task {
            context.SetDefaultTimeout 8000.0f
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
