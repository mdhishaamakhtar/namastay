module Namastay.Client.App

open Elmish
open Elmish.React
open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages
open Namastay.Client.Routing
open Namastay.Client.Routing.Page
open Namastay.Client.Theme
open Namastay.Shared

[<RequireQualifiedAccess>]
type Screen =
    | Home of Home.State.Model
    | Journey of Journey.State.Model
    | Trip of Trip.State.Model
    | SignIn of SignIn.State.Model
    | MyTrips of MyTrips.State.Model
    | Account of Account.State.Model
    | Reading of ReadingPage
    | Credits of Credits.State.Model
    | Edit of Edit.State.Model
    | Browse of Browse.State.Model
    | NotFound

type Model = { Screen: Screen }

type Msg =
    | UrlChanged of string list
    | HomeMsg of Home.State.Msg
    | JourneyMsg of Journey.State.Msg
    | TripMsg of Trip.State.Msg
    | SignInMsg of SignIn.State.Msg
    | MyTripsMsg of MyTrips.State.Msg
    | AccountMsg of Account.State.Msg
    | CreditsMsg of Credits.State.Msg
    | EditMsg of Edit.State.Msg
    | BrowseMsg of Browse.State.Msg

let private showTrip source =
    let trip, command = Trip.State.init source
    { Screen = Screen.Trip trip }, Cmd.map TripMsg command

let private show (segments: string list) =
    match parse segments with
    | Page.Home ->
        let home, command = Home.State.init ()
        { Screen = Screen.Home home }, Cmd.map HomeMsg command
    | Page.Journey ->
        let journey, command = Journey.State.init ()
        { Screen = Screen.Journey journey }, Cmd.map JourneyMsg command
    | Page.NotFound -> { Screen = Screen.NotFound }, Cmd.none
    | Page.Trip token -> showTrip (Trip.State.Source.Shared token)
    | Page.MyTrip id -> showTrip (Trip.State.Source.Mine id)
    | Page.SignIn ->
        let signIn, command = SignIn.State.init ()
        { Screen = Screen.SignIn signIn }, Cmd.map SignInMsg command
    | Page.MyTrips ->
        let trips, command = MyTrips.State.init ()
        { Screen = Screen.MyTrips trips }, Cmd.map MyTripsMsg command
    | Page.Account ->
        let account, command = Account.State.init ()
        { Screen = Screen.Account account }, Cmd.map AccountMsg command
    | Page.Reading page -> { Screen = Screen.Reading page }, Cmd.none
    | Page.Credits ->
        let credits, command = Credits.State.init ()
        { Screen = Screen.Credits credits }, Cmd.map CreditsMsg command
    | Page.Browse target ->
        let browse, command = Browse.State.init target
        { Screen = Screen.Browse browse }, Cmd.map BrowseMsg command
    | Page.EditTrip id ->
        let edit, command = Edit.State.init id
        { Screen = Screen.Edit edit }, Cmd.map EditMsg command

/// Each page starts from brand colours, and sets its own if it has them.
let private showFresh segments =
    let model, command = show segments
    model, Cmd.batch [ Cmd.ofEffect (fun _ -> Theme.reset ()); command ]

/// The app is running now, so whatever the saved page held back can show.
let private revealPage () =
    Browser.Dom.document.documentElement.removeAttribute "data-returning"

let init () =
    let model, command = showFresh (Browser.currentPath ())
    model, Cmd.batch [ Cmd.ofEffect (fun _ -> revealPage ()); command ]

let update msg model =
    match msg, model.Screen with
    | UrlChanged segments, _ ->
        // Pages reached from here are new, so they can fade in. The saved one never replays it.
        Browser.Dom.document.documentElement.removeAttribute "data-seeded"
        showFresh segments
    | HomeMsg inner, Screen.Home home ->
        let updated, command = Home.State.update inner home
        { Screen = Screen.Home updated }, Cmd.map HomeMsg command
    | JourneyMsg inner, Screen.Journey journey ->
        let updated, command = Journey.State.update inner journey
        { Screen = Screen.Journey updated }, Cmd.map JourneyMsg command
    | TripMsg inner, Screen.Trip trip ->
        let updated, command = Trip.State.update inner trip
        { Screen = Screen.Trip updated }, Cmd.map TripMsg command
    | SignInMsg inner, Screen.SignIn signIn ->
        let updated, command = SignIn.State.update inner signIn
        { Screen = Screen.SignIn updated }, Cmd.map SignInMsg command
    | MyTripsMsg inner, Screen.MyTrips trips ->
        let updated, command = MyTrips.State.update inner trips
        { Screen = Screen.MyTrips updated }, Cmd.map MyTripsMsg command
    | CreditsMsg inner, Screen.Credits credits ->
        let updated, command = Credits.State.update inner credits
        { Screen = Screen.Credits updated }, Cmd.map CreditsMsg command
    | AccountMsg inner, Screen.Account account ->
        let updated, command = Account.State.update inner account
        { Screen = Screen.Account updated }, Cmd.map AccountMsg command
    | EditMsg inner, Screen.Edit edit ->
        let updated, command = Edit.State.update inner edit
        { Screen = Screen.Edit updated }, Cmd.map EditMsg command
    | BrowseMsg inner, Screen.Browse browse ->
        let updated, command = Browse.State.update inner browse
        { Screen = Screen.Browse updated }, Cmd.map BrowseMsg command
    | HomeMsg _, _
    | JourneyMsg _, _
    | TripMsg _, _
    | SignInMsg _, _
    | MyTripsMsg _, _
    | AccountMsg _, _
    | CreditsMsg _, _
    | EditMsg _, _
    | BrowseMsg _, _ -> model, Cmd.none

let private personal (title: string) (page: ReactElement) =
    Html.div [
        prop.style [ style.display.contents ]
        prop.children [ Seo.Private title; page ]
    ]

/// A page people read gets the site's footer. Tasks like swiping keep the screen to themselves.
let private withFooter = Footer.around

let view model dispatch =
    match model.Screen with
    | Screen.Home home -> withFooter (Home.View.view home (HomeMsg >> dispatch) Browser.navigate)
    | Screen.Journey journey ->
        personal "Plan your trip | Namastay" (Journey.View.view journey (JourneyMsg >> dispatch))
    | Screen.Trip trip -> personal "Your trip | Namastay" (Trip.View.view trip (TripMsg >> dispatch))
    | Screen.SignIn signIn -> personal "Sign in | Namastay" (SignIn.View.view signIn (SignInMsg >> dispatch))
    | Screen.MyTrips trips ->
        personal "My trips | Namastay" (withFooter (MyTrips.View.view trips (MyTripsMsg >> dispatch)))
    | Screen.Account account ->
        personal "Your account | Namastay" (withFooter (Account.View.view account (AccountMsg >> dispatch)))
    | Screen.Edit edit -> personal "Change your trip | Namastay" (Edit.View.view edit (EditMsg >> dispatch))
    | Screen.Browse browse -> withFooter (Browse.View.view browse (BrowseMsg >> dispatch))
    | Screen.Reading page -> withFooter (Reading.View.view page)
    | Screen.Credits credits -> withFooter (Credits.View.view credits (CreditsMsg >> dispatch))
    | Screen.NotFound -> personal "Page not found | Namastay" (withFooter NotFound.view)

let subscribe (_: Model) : Sub<Msg> = [ [ "url" ], (fun dispatch -> Browser.subscribe (UrlChanged >> dispatch)) ]

Program.mkProgram init update view
|> Program.withSubscription subscribe
|> Program.withReactSynchronous "app"
|> Program.run
