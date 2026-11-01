namespace Namastay.E2ETests

open System
open System.Text.Json
open Microsoft.Playwright
open Xunit

type AccountTests(browser: BrowserFixture) =
    let password = "correct horse battery"

    let email () = $"{Guid.NewGuid():N}@example.org"

    let button (page: IPage) (name: string) =
        page.GetByRole(AriaRole.Button, PageGetByRoleOptions(Name = name, Exact = true))

    let link (page: IPage) (name: string) =
        page.GetByRole(AriaRole.Link, PageGetByRoleOptions(Name = name, Exact = true))

    let openNewTrip (page: IPage) =
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
            let token = json.RootElement.GetProperty("shareToken").GetString()
            let! _ = page.GotoAsync $"{browser.BaseUrl}/trip/{token}"
            do! page.Locator(".stop-head").First.WaitForAsync()
        }

    let fillAccount (page: IPage) (address: string) =
        task {
            do! page.GetByLabel("Email").FillAsync address
            do! page.GetByLabel("Password").FillAsync password
        }

    /// Plans a trip, then signs up from the save button and lands on the saved copy.
    let saveNewTrip () =
        task {
            let! page = browser.NewPage()
            do! openNewTrip page
            do! (button page "Save and change this trip").ClickAsync()
            do! page.WaitForURLAsync "**/sign-in**"
            do! (button page "Create an account").ClickAsync()
            do! fillAccount page (email ())
            do! (button page "Create account").ClickAsync()
            do! page.WaitForURLAsync "**/trip/**"
            do! (button page "Save and change this trip").ClickAsync()
            do! page.WaitForURLAsync "**/my/**"
            do! page.Locator(".stop-head").First.WaitForAsync()
            return page
        }

    [<Fact>]
    member _.``my trips asks a signed-out visitor to sign in``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync $"{browser.BaseUrl}/my"
            do! page.WaitForURLAsync "**/sign-in?next=%2Fmy"
        }

    [<Fact>]
    member _.``wrong password shows an error``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync $"{browser.BaseUrl}/sign-in"
            do! fillAccount page (email ())
            do! (button page "Sign in").ClickAsync()
            let alert = page.GetByRole AriaRole.Alert
            do! alert.WaitForAsync()
            let! text = alert.TextContentAsync()
            Assert.Contains("isn't right", text)
        }

    [<Fact>]
    member _.``a short password is explained at sign up``() =
        task {
            let! page = browser.NewPage()
            let! _ = page.GotoAsync $"{browser.BaseUrl}/sign-in"
            do! (button page "Create an account").ClickAsync()
            do! page.GetByLabel("Email").FillAsync(email ())
            do! page.GetByLabel("Password").FillAsync "short"
            do! (button page "Create account").ClickAsync()
            do! page.Locator(".form-error").WaitForAsync()
        }

    [<Fact>]
    member _.``saved trip appears in my trips``() =
        task {
            let! page = saveNewTrip ()
            do! (link page "My trips").ClickAsync()
            do! page.Locator(".my-trip a").WaitForAsync()
        }

    [<Fact>]
    member _.``my trips starts a new trip``() =
        task {
            let! page = saveNewTrip ()
            do! (link page "My trips").ClickAsync()
            do! (link page "Plan a new trip").ClickAsync()
            do! page.WaitForURLAsync "**/journey"
        }

    [<Fact>]
    member _.``renaming a trip shows on the saved copy``() =
        task {
            let! page = saveNewTrip ()
            do! (link page "Change this trip").ClickAsync()
            do! page.GetByLabel("Trip name").FillAsync "Our honeymoon"
            do! (button page "Save changes").ClickAsync()
            do! page.WaitForURLAsync "**/my/*"
            do! page.GetByRole(AriaRole.Heading, PageGetByRoleOptions(Name = "Our honeymoon")).WaitForAsync()
        }

    [<Fact>]
    member _.``own stop warns and stays in the trip``() =
        task {
            let! page = saveNewTrip ()
            do! (link page "Change this trip").ClickAsync()
            do! page.GetByLabel("Or somewhere of your own").FillAsync "Asha's house"
            do! (button page "Add this stop").ClickAsync()
            do! (button page "Save changes").ClickAsync()
            do! page.Locator(".stop-warning").WaitForAsync()
            do! (link page "See your trip").ClickAsync()
            do! page.GetByText("Asha's house").First.WaitForAsync()
        }

    [<Fact>]
    member _.``an edit that breaks the rules shows why``() =
        task {
            let! page = saveNewTrip ()
            do! (link page "Change this trip").ClickAsync()
            do! page.GetByLabel("Trip name").FillAsync ""
            do! (button page "Save changes").ClickAsync()
            do! page.Locator(".form-error").WaitForAsync()
            Assert.EndsWith("/edit", page.Url)
        }

    [<Fact>]
    member _.``deleting takes a second press``() =
        task {
            let! page = saveNewTrip ()
            do! (link page "My trips").ClickAsync()
            do! (button page "Delete").ClickAsync()
            do! (button page "Delete this trip?").ClickAsync()
            do! page.GetByText("You haven't saved a trip yet.").WaitForAsync()
        }

    [<Fact>]
    member _.``printing shows every stop and hides the buttons``() =
        task {
            let! page = browser.NewPage()
            do! openNewTrip page
            let! stops = page.Locator(".stop-head").CountAsync()
            do! page.EmulateMediaAsync(PageEmulateMediaOptions(Media = Media.Print))
            let! visible = page.Locator(".stop-body:visible").CountAsync()
            let! shareButtons = page.Locator(".trip-share:visible").CountAsync()
            Assert.Equal(stops, visible)
            Assert.Equal(0, shareButtons)
        }

    [<Fact>]
    member _.``edit page fits a phone``() =
        task {
            let! page = saveNewTrip ()
            do! (link page "Change this trip").ClickAsync()
            do! page.GetByLabel("Trip name").WaitForAsync()

            let! overflow =
                page.EvaluateAsync<bool> "document.documentElement.scrollWidth > window.innerWidth"

            Assert.False overflow
        }

    interface IClassFixture<BrowserFixture>
