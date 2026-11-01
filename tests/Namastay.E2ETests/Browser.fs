namespace Namastay.E2ETests

open System
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

/// One Chromium for the run, sized like a phone. Needs `make up`, `make sample`, `make dev` and `make client`.
type BrowserFixture() =
    let mutable playwright: IPlaywright = null
    let mutable browser: IBrowser = null

    member _.BaseUrl =
        match Environment.GetEnvironmentVariable "NAMASTAY_URL" with
        | null
        | "" -> "http://localhost:5173"
        | value -> value

    member _.NewReducedMotionPage() =
        task {
            let! context =
                browser.NewContextAsync(BrowserNewContextOptions(ReducedMotion = ReducedMotion.Reduce))

            return! context.NewPageAsync()
        }

    member _.NewPage() =
        task {
            let! context =
                browser.NewContextAsync(
                    BrowserNewContextOptions(ViewportSize = ViewportSize(Width = 390, Height = 844), HasTouch = true)
                )

            return! context.NewPageAsync()
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

[<CollectionDefinition("browser")>]
type BrowserCollection() =
    interface ICollectionFixture<BrowserFixture>
