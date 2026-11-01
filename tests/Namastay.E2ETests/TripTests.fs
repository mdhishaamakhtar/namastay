namespace Namastay.E2ETests

open System
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

type TripTests(browser: BrowserFixture) =
    let firstStop (json: JsonNode) : JsonNode =
        json["trip"].Item("stops").AsArray().Item 0

    let createTrip (page: IPage) =
        task {
            let body =
                """{"answers":[{"card":"green-valleys","liked":true}],"days":7,"month":null,"travellers":"Solo","budget":"Mid","transportComfort":"Anything"}"""

            let! response =
                page.APIRequest.PostAsync(
                    $"{browser.BaseUrl}/api/itineraries",
                    APIRequestContextOptions(Data = body, Headers = dict [ "Content-Type", "application/json" ])
                )

            let! text = response.TextAsync()
            use json = JsonDocument.Parse text
            return json.RootElement.GetProperty("shareToken").GetString()
        }

    let openTrip () =
        task {
            let! page = browser.NewPage()
            let! token = createTrip page
            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
            return page, token
        }

    /// Serves a changed copy of the real trip, to show states the sample data can't reach.
    let openChangedTrip (change: JsonNode -> unit) =
        task {
            let! page = browser.NewPage()
            let! token = createTrip page

            let! _ =
                page.RouteAsync(
                    $"**/api/itineraries/{token}",
                    Func<IRoute, Task>(fun route ->
                        task {
                            let! response = route.FetchAsync()
                            let! text = response.TextAsync()
                            let json = JsonNode.Parse text
                            change json
                            do! route.FulfillAsync(RouteFulfillOptions(Json = json))
                        })
                )

            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
            return page
        }

    [<Fact>]
    member _.``a slow trip shows the page skeleton``() =
        task {
            let! page = browser.NewPage()
            let! token = createTrip page

            let! _ =
                page.RouteAsync("**/api/itineraries/*", Func<IRoute, Task>(fun _ -> Task.CompletedTask))

            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.Locator(".trip .bone").First.WaitForAsync()
            let! stops = page.Locator(".trip-stops .stop-card").CountAsync()
            Assert.True(stops > 0)
        }

    [<Fact>]
    member _.``a loading trip waits in a neutral colour``() =
        task {
            let! page = browser.NewPage()
            let! token = createTrip page

            let! _ =
                page.RouteAsync($"**/api/itineraries/{token}", Func<IRoute, Task>(fun _ -> Task.CompletedTask))

            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.Locator(".trip .bone").First.WaitForAsync()

            let! colour =
                page.EvaluateAsync<string> "getComputedStyle(document.querySelector('.trip-panel')).backgroundColor"

            Assert.Equal("rgb(239, 230, 208)", colour)
        }

    [<Fact>]
    member _.``first stop starts open``() =
        task {
            let! page, _ = openTrip ()
            do! page.Locator(".stop-body").First.WaitForAsync()
        }

    [<Fact>]
    member _.``stop expands on tap``() =
        task {
            let! page, _ = openTrip ()
            let head = page.Locator(".stop-head").First
            do! head.ClickAsync()
            let! closed = page.Locator(".stop-open").CountAsync()
            do! head.ClickAsync()
            let! opened = page.Locator(".stop-open").CountAsync()
            Assert.Equal(0, closed)
            Assert.Equal(1, opened)
        }

    [<Fact>]
    member _.``full guide link opens guide``() =
        task {
            let! page, _ = openTrip ()
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Read the full guide")).ClickAsync()
            do! page.WaitForURLAsync "**/places/**"
        }

    [<Fact>]
    member _.``back from guide restores trip``() =
        task {
            let! page, token = openTrip ()
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Read the full guide")).ClickAsync()
            do! page.WaitForURLAsync "**/places/**"
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Your trip")).ClickAsync()
            do! page.WaitForURLAsync $"**/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
        }

    [<Fact>]
    member _.``browser back returns from guide``() =
        task {
            let! page, token = openTrip ()
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Read the full guide")).ClickAsync()
            do! page.WaitForURLAsync "**/places/**"
            let! _ = page.GoBackAsync()
            do! page.WaitForURLAsync $"**/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
        }

    [<Fact>]
    member _.``stops use own family colours``() =
        task {
            let! page, _ = openTrip ()

            let! colour =
                page.EvaluateAsync<string> "getComputedStyle(document.querySelector('.stop-head')).backgroundColor"

            Assert.Equal("rgb(47, 93, 58)", colour)
        }

    [<Fact>]
    member _.``panel takes the base family colour``() =
        task {
            let! page, _ = openTrip ()

            let! colour =
                page.EvaluateAsync<string> "getComputedStyle(document.querySelector('.trip-panel')).backgroundColor"

            Assert.Equal("rgb(47, 93, 58)", colour)
        }

    [<Fact>]
    member _.``leaving a trip restores brand colours``() =
        task {
            let! page, _ = openTrip ()
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Namastay")).ClickAsync()

            let! colour =
                page.EvaluateAsync<string> "document.documentElement.style.getPropertyValue('--deep')"

            Assert.Equal("", colour)
        }

    [<Fact>]
    member _.``share copies link``() =
        task {
            let! page, token = openTrip ()
            do! page.Context.GrantPermissionsAsync [ "clipboard-read"; "clipboard-write" ]
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Copy your trip link")).ClickAsync()
            do! page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = "Link copied")).WaitForAsync()
            let! copied = page.EvaluateAsync<string> "navigator.clipboard.readText()"
            Assert.Contains(token, copied)
        }

    [<Fact>]
    member _.``retired stop shows message``() =
        task {
            let! page =
                openChangedTrip (fun (json: JsonNode) -> (firstStop json).Item("retired") <- JsonValue.Create true)

            do! page.GetByText("This place is no longer in our guide.").WaitForAsync()

            let! links =
                page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Read the full guide")).CountAsync()

            Assert.Equal(0, links)
        }

    [<Fact>]
    member _.``swap reason is shown``() =
        task {
            let swap =
                JsonNode.Parse """{"from":"Goa","to":"Varkala","reason":"Goa isn't a good choice in July."}"""

            let! page =
                openChangedTrip (fun (json: JsonNode) -> json["trip"]["swaps"] <- JsonArray(swap))

            do! page.GetByText("Goa isn't a good choice in July.").WaitForAsync()
        }

    [<Fact>]
    member _.``left out interests become reasons to come back``() =
        task {
            let! page =
                openChangedTrip (fun (json: JsonNode) ->
                    json["trip"]["leftOut"] <- JsonArray(JsonValue.Create "Nightlife"))

            do! page.GetByText("Reasons to come back").WaitForAsync()
        }

    [<Fact>]
    member _.``notices fold into one line that names a permit``() =
        task {
            let! page =
                openChangedTrip (fun (json: JsonNode) ->
                    let stop = firstStop json
                    let slug = stop.Item("placeSlug").GetValue<string>()

                    json["trip"]["notices"] <-
                        JsonArray(
                            JsonNode.Parse(
                                $$"""{"placeSlug":"{{slug}}","kind":"Permit","severity":"Permit","body":"You need a permit."}"""
                            )
                        ))

            do! page.GetByText("You need a permit, and a few things to know").ClickAsync()
            do! page.Locator(".stop-notice", PageLocatorOptions(HasText = "You need a permit.")).WaitForAsync()
        }

    [<Fact>]
    member _.``days are numbered across the whole trip``() =
        task {
            let! page, _ = openTrip ()
            let last = page.Locator(".stop-head").Last
            do! last.ClickAsync()
            do! page.GetByText("Day 7").WaitForAsync()
        }

    [<Fact>]
    member _.``no sideways scroll on a phone``() =
        task {
            let! page, _ = openTrip ()

            let! wide =
                page.EvaluateAsync<bool> "document.documentElement.scrollWidth > window.innerWidth"

            Assert.False wide
        }

    interface IClassFixture<BrowserFixture>
