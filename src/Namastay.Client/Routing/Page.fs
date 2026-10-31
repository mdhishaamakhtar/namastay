module Namastay.Client.Routing.Page

[<RequireQualifiedAccess>]
type Page =
    | Home
    | Journey
    | Trip of shareToken: string
    | NotFound

let parse (segments: string list) =
    match segments with
    | [] -> Page.Home
    | [ "journey" ] -> Page.Journey
    | [ "trip"; token ] -> Page.Trip token
    | _ -> Page.NotFound
