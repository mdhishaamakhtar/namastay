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

    let openTrip () =
        task {
            let! page = browser.NewPage()
            let! token = browser.CreateTrip page
            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
            return page, token
        }

    /// Serves a changed copy of the real trip, to show states the sample data can't reach.
    let openChangedTrip (change: JsonNode -> unit) =
        task {
            let! page = browser.NewPage()
            let! token = browser.CreateTrip page

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

    /// Opens the calendar on next month, so every day in it can still be chosen.
    let openCalendar (page: IPage) =
        task {
            do! page.Locator(".trip-start-button").ClickAsync()
            do! page.GetByLabel("Later month").ClickAsync()
            return page.Locator ".calendar-day"
        }

    [<Fact>]
    member _.``a stop says why it is in the trip, under its name``() =
        task {
            let! page, _ = openTrip ()
            let why = page.Locator(".stop-head .stop-why").First
            do! Assertions.Expect(why).ToHaveTextAsync "Because you liked “Green valleys”"

            let! name =
                page.EvaluateAsync<float> "document.querySelector('.stop-title strong').getBoundingClientRect().left"

            let! reason =
                page.EvaluateAsync<float> "document.querySelector('.stop-why').getBoundingClientRect().left"

            Assert.Equal(name, reason)
        }

    [<Fact>]
    member _.``picking the day you land puts dates on the stops and in the link``() =
        task {
            let! page, token = openTrip ()
            let stay = page.Locator(".stop-title small").First
            let! before = stay.TextContentAsync()
            let! days = openCalendar page
            let! id = days.First.GetAttributeAsync "id"
            let chosen = id.Replace("day-", "")
            do! days.First.ClickAsync()
            do! Assertions.Expect(page).ToHaveURLAsync $"{browser.BaseUrl}/trip/{token}?from={chosen}"
            do! Assertions.Expect(stay).Not.ToHaveTextAsync before
            do! Assertions.Expect(page.Locator ".calendar").ToHaveCountAsync 0
        }

    [<Fact>]
    member _.``the calendar marks every day of the trip from the day you land``() =
        task {
            let! page, _ = openTrip ()
            let! days = openCalendar page
            do! days.First.ClickAsync()
            do! page.Locator(".trip-start-button").ClickAsync()
            let! stops = page.Locator(".stop-title small").AllTextContentsAsync()
            do! Assertions.Expect(page.Locator ".calendar-start").ToHaveCountAsync 1
            let! marked = page.Locator(".calendar-trip").CountAsync()
            Assert.True(marked >= stops.Count, $"{marked} days marked for {stops.Count} stops")
        }

    [<Fact>]
    member _.``the arrow keys move through the calendar's days``() =
        task {
            let! page, _ = openTrip ()
            let! days = openCalendar page
            do! days.First.FocusAsync()
            do! page.Keyboard.PressAsync "ArrowDown"
            do! Assertions.Expect(days.Nth 7).ToBeFocusedAsync()
            do! page.Keyboard.PressAsync "ArrowLeft"
            do! Assertions.Expect(days.Nth 6).ToBeFocusedAsync()
            do! page.Keyboard.PressAsync "Escape"
            do! Assertions.Expect(page.Locator ".calendar").ToHaveCountAsync 0
            do! Assertions.Expect(page.Locator ".trip-start-button").ToBeFocusedAsync()
            do! page.Keyboard.PressAsync "Enter"
            do! page.GetByLabel("Later month").FocusAsync()
            do! page.Keyboard.PressAsync "Escape"
            do! Assertions.Expect(page.Locator ".calendar").ToHaveCountAsync 0
        }

    [<Fact>]
    member _.``removing the dates goes back to day numbers``() =
        task {
            let! page, token = openTrip ()
            let! days = openCalendar page
            do! days.First.ClickAsync()
            do! page.Locator(".trip-start-clear").ClickAsync()
            do! Assertions.Expect(page).ToHaveURLAsync $"{browser.BaseUrl}/trip/{token}"
            do! Assertions.Expect(page.Locator(".trip-start-button span").First).ToHaveTextAsync "Pick a day"
        }

    [<Fact>]
    member _.``a link that carries the landing day opens with its dates``() =
        task {
            let! page, token = openTrip ()
            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}?from=2027-02-06"
            do! Assertions.Expect(page.Locator(".stop-title small").First).ToContainTextAsync "Sat 6"
            do! Assertions.Expect(page.Locator(".trip-start-button span").First).ToHaveTextAsync "Sat 6 Feb 2027"
            do! page.Locator(".trip-start-button").ClickAsync()
            do! Assertions.Expect(page.Locator ".calendar-head strong").ToHaveTextAsync "February 2027"

        }

    [<Fact>]
    member _.``saying a trip feels right is thanked, and asked only once``() =
        task {
            let! page, _ = openTrip ()
            let asked = page.Locator ".trip-reaction"
            do! Assertions.Expect(asked.Locator "button").ToHaveCountAsync 2
            do! asked.GetByText("Yes, it does").ClickAsync()
            do! Assertions.Expect(asked).ToContainTextAsync "Thank you"
            do! Assertions.Expect(asked.Locator "button").ToHaveCountAsync 0
        }

    [<Fact>]
    member _.``a stop says what there is to do, on the same lines as its days``() =
        task {
            let! page, _ = openTrip ()
            let things = page.Locator(".stop-open .stop-do").First
            do! Assertions.Expect(things.Locator("strong").First).ToBeVisibleAsync()
        }

    [<Fact>]
    member _.``the route is drawn with a numbered dot for every stop``() =
        task {
            let! page, _ = openTrip ()
            let! stops = page.Locator(".route li").CountAsync()
            do! Assertions.Expect(page.Locator ".route-sketch .route-stop").ToHaveCountAsync stops
            do! Assertions.Expect(page.Locator(".route-sketch .route-land").First).ToBeAttachedAsync()
            do! Assertions.Expect(page.Locator(".route-sketch .route-number").First).ToHaveTextAsync "1"
            do! Assertions.Expect(page.Locator(".route li .route-dot").First).ToHaveTextAsync "1"
        }

    [<Fact>]
    member _.``a trip saved before the map existed lists its stops without a drawing``() =
        task {
            let! page =
                openChangedTrip (fun json ->
                    for stop in json["trip"].Item("stops").AsArray() do
                        stop.AsObject().["position"] <- null)

            do! Assertions.Expect(page.Locator(".route li").First).ToBeVisibleAsync()
            do! Assertions.Expect(page.Locator "svg.route-sketch").ToHaveCountAsync 0
        }

    [<Fact>]
    member _.``a trip saved before reasons existed shows none``() =
        task {
            let! page =
                openChangedTrip (fun json -> (firstStop json).AsObject().Remove "reason" |> ignore)

            do! Assertions.Expect(page.Locator(".stop-title strong").First).ToBeVisibleAsync()
            let! first = page.Locator(".stop-head").First.Locator(".stop-why").CountAsync()
            Assert.Equal(0, first)
        }

    [<Fact>]
    member _.``a slow trip shows the page skeleton``() =
        task {
            let! page = browser.NewPage()
            let! token = browser.CreateTrip page

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
            let! token = browser.CreateTrip page

            let! _ =
                page.RouteAsync($"**/api/itineraries/{token}", Func<IRoute, Task>(fun _ -> Task.CompletedTask))

            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.Locator(".trip .bone").First.WaitForAsync()

            do!
                Assertions
                    .Expect(page.Locator(".trip-panel").First)
                    .ToHaveCSSAsync("background-color", "rgb(239, 230, 208)")
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
            do! waitForAddress page "**/places/**"
        }

    [<Fact>]
    member _.``back from guide restores trip``() =
        task {
            let! page, token = openTrip ()
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Read the full guide")).ClickAsync()
            do! waitForAddress page "**/places/**"
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Your trip")).ClickAsync()
            do! waitForAddress page $"**/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
        }

    [<Fact>]
    member _.``browser back returns from guide``() =
        task {
            let! page, token = openTrip ()
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Read the full guide")).ClickAsync()
            do! waitForAddress page "**/places/**"
            let! _ = page.GoBackAsync()
            do! waitForAddress page $"**/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
        }

    [<Fact>]
    member _.``stops use own family colours``() =
        task {
            let! page, _ = openTrip ()

            do!
                Assertions
                    .Expect(page.Locator(".stop-head").First)
                    .ToHaveCSSAsync("background-color", "rgb(47, 93, 58)")
        }

    [<Fact>]
    member _.``panel takes the base family colour``() =
        task {
            let! page, _ = openTrip ()

            do!
                Assertions
                    .Expect(page.Locator(".trip-panel").First)
                    .ToHaveCSSAsync("background-color", "rgb(47, 93, 58)")
        }

    [<Fact>]
    member _.``leaving a trip restores brand colours``() =
        task {
            let! page, _ = openTrip ()
            do! page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = "Namastay")).ClickAsync()

            let! _ =
                page.WaitForFunctionAsync "document.documentElement.style.getPropertyValue('--deep') === ''"

            ()
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
