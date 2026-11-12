namespace Namastay.E2ETests

open System
open System.Text.Json
open System.Text.RegularExpressions
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

[<AutoOpen>]
module Addresses =
    let private stepTimeout = 8000.0f

    /// Waits until the page's address matches `glob`, however it got there. The site changes
    /// pages without loading one, and that can be over before a wait for navigation starts
    /// listening. Such a wait then never hears it and times out on a page that's already right.
    let waitForAddress (page: IPage) (glob: string) : Task =
        let pattern =
            "^" + Regex.Escape(glob).Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "$"

        Assertions.Expect(page).ToHaveURLAsync(Regex pattern, PageAssertionsToHaveURLOptions(Timeout = stepTimeout))

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

    /// A trip made through the API, so tests that start on a trip don't swipe to get there.
    member this.CreateTrip(page: IPage) =
        task {
            let body =
                """{"answers":[{"card":"green-valleys","liked":true}],"days":7,"month":null,"travellers":"Solo","budget":"Mid","transportComfort":"Anything","scope":null,"diet":null}"""

            let! response =
                page.APIRequest.PostAsync(
                    $"{this.BaseUrl}/api/itineraries",
                    APIRequestContextOptions(Data = body, Headers = dict [ "Content-Type", "application/json" ])
                )

            Assert.True(response.Ok, "the trip request failed")
            let! text = response.TextAsync()
            use json = JsonDocument.Parse text
            return json.RootElement.GetProperty("shareToken").GetString()
        }

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
