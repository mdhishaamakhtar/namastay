module Namastay.Client.App

open Elmish
open Elmish.React
open Feliz
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

let init () = showFresh (Browser.currentPath ())

let update msg model =
    match msg, model.Screen with
    | UrlChanged segments, _ -> showFresh segments
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
    | EditMsg _, _
    | BrowseMsg _, _ -> model, Cmd.none

let private notFound () =
    Html.main [
        Html.h1 "We can't find that page"
        Html.a [
            prop.className "button"
            prop.href Routes.home.Template
            prop.text "Back to Namastay"
        ]
    ]

let view model dispatch =
    match model.Screen with
    | Screen.Home home -> Home.View.view home (HomeMsg >> dispatch) Browser.navigate
    | Screen.Journey journey -> Journey.View.view journey (JourneyMsg >> dispatch)
    | Screen.Trip trip -> Trip.View.view trip (TripMsg >> dispatch)
    | Screen.SignIn signIn -> SignIn.View.view signIn (SignInMsg >> dispatch)
    | Screen.MyTrips trips -> MyTrips.View.view trips (MyTripsMsg >> dispatch)
    | Screen.Edit edit -> Edit.View.view edit (EditMsg >> dispatch)
    | Screen.Browse browse -> Browse.View.view browse (BrowseMsg >> dispatch)
    | Screen.NotFound -> notFound ()

let subscribe (_: Model) : Sub<Msg> = [ [ "url" ], (fun dispatch -> Browser.subscribe (UrlChanged >> dispatch)) ]

Program.mkProgram init update view
|> Program.withSubscription subscribe
|> Program.withReactSynchronous "app"
|> Program.run
