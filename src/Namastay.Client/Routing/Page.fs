module Namastay.Client.Routing.Page

/// The pages you browse from, each found by what's in its address.
[<RequireQualifiedAccess>]
type Browse =
    | Places
    | Food
    | Region of slug: string
    | FoodRegion of slug: string
    | Feeling of slug: string
    | Dish of slug: string

[<RequireQualifiedAccess>]
type Page =
    | Home
    | Journey
    | Trip of shareToken: string
    | SignIn
    | MyTrips
    | Account
    | MyTrip of id: string
    | EditTrip of id: string
    | Browse of Browse
    | NotFound

let parse (segments: string list) =
    match segments with
    | [] -> Page.Home
    | [ "journey" ] -> Page.Journey
    | [ "trip"; token ] -> Page.Trip token
    | [ "sign-in" ] -> Page.SignIn
    | [ "my" ] -> Page.MyTrips
    | [ "account" ] -> Page.Account
    | [ "my"; id ] -> Page.MyTrip id
    | [ "my"; id; "edit" ] -> Page.EditTrip id
    | [ "explore" ] -> Page.Browse Browse.Places
    | [ "explore"; slug ] -> Page.Browse(Browse.Region slug)
    | [ "food" ] -> Page.Browse Browse.Food
    | [ "food"; slug ] -> Page.Browse(Browse.FoodRegion slug)
    | [ "feelings"; slug ] -> Page.Browse(Browse.Feeling slug)
    | [ "dishes"; slug ] -> Page.Browse(Browse.Dish slug)
    | _ -> Page.NotFound
