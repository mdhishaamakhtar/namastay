module Namastay.Client.App

open Elmish
open Elmish.React
open Feliz
open Namastay.Client.Routing

type Model = { Url: string list }

type Msg = UrlChanged of string list

let init () =
    { Url = Browser.currentPath () }, Cmd.none

let update (UrlChanged url) (_: Model) = { Url = url }, Cmd.none

let view (_: Model) (_: Msg -> unit) = Html.main [ Html.h1 "Namastay" ]

let subscribe (_: Model) : Sub<Msg> = [ [ "url" ], (fun dispatch -> Browser.subscribe (UrlChanged >> dispatch)) ]

Program.mkProgram init update view
|> Program.withSubscription subscribe
|> Program.withReactSynchronous "app"
|> Program.run
