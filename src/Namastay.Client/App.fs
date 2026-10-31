module Namastay.Client.App

open Elmish
open Elmish.React
open Feliz
open Namastay.Client.Pages
open Namastay.Client.Routing
open Namastay.Client.Routing.Page
open Namastay.Shared

[<RequireQualifiedAccess>]
type Screen =
    | Home of Home.State.Model
    | Journey
    | Trip of Trip.State.Model
    | NotFound

type Model = { Screen: Screen }

type Msg =
    | UrlChanged of string list
    | HomeMsg of Home.State.Msg
    | TripMsg of Trip.State.Msg

let private show (segments: string list) =
    match parse segments with
    | Page.Home ->
        let home, command = Home.State.init ()
        { Screen = Screen.Home home }, Cmd.map HomeMsg command
    | Page.Journey -> { Screen = Screen.Journey }, Cmd.none
    | Page.NotFound -> { Screen = Screen.NotFound }, Cmd.none
    | Page.Trip token ->
        let trip, command = Trip.State.init token
        { Screen = Screen.Trip trip }, Cmd.map TripMsg command

let init () = show (Browser.currentPath ())

let update msg model =
    match msg, model.Screen with
    | UrlChanged segments, _ -> show segments
    | HomeMsg inner, Screen.Home home ->
        let updated, command = Home.State.update inner home
        { Screen = Screen.Home updated }, Cmd.map HomeMsg command
    | TripMsg inner, Screen.Trip trip ->
        let updated, command = Trip.State.update inner trip
        { Screen = Screen.Trip updated }, Cmd.map TripMsg command
    | HomeMsg _, _
    | TripMsg _, _ -> model, Cmd.none

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
    | Screen.Journey -> Journey.View.view ()
    | Screen.Trip trip -> Trip.View.view trip (TripMsg >> dispatch)
    | Screen.NotFound -> notFound ()

let subscribe (_: Model) : Sub<Msg> = [ [ "url" ], (fun dispatch -> Browser.subscribe (UrlChanged >> dispatch)) ]

Program.mkProgram init update view
|> Program.withSubscription subscribe
|> Program.withReactSynchronous "app"
|> Program.run
