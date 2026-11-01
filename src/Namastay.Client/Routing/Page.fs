module Namastay.Client.Routing.Page

[<RequireQualifiedAccess>]
type Page =
    | Home
    | Journey
    | Trip of shareToken: string
    | SignIn
    | MyTrips
    | MyTrip of id: string
    | EditTrip of id: string
    | NotFound

let parse (segments: string list) =
    match segments with
    | [] -> Page.Home
    | [ "journey" ] -> Page.Journey
    | [ "trip"; token ] -> Page.Trip token
    | [ "sign-in" ] -> Page.SignIn
    | [ "my" ] -> Page.MyTrips
    | [ "my"; id ] -> Page.MyTrip id
    | [ "my"; id; "edit" ] -> Page.EditTrip id
    | _ -> Page.NotFound
