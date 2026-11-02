module Namastay.Client.Routing.Page

open Namastay.Shared

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
type ReadingPage =
    | Privacy
    | Terms
    | Contact

[<RequireQualifiedAccess>]
type Page =
    | Home
    | Journey
    | Trip of shareToken: string
    | SignIn
    | MyTrips
    | Account
    | Reading of ReadingPage
    | Credits
    | MyTrip of id: string
    | EditTrip of id: string
    | Browse of Browse
    | NotFound

let private (|At|_|) route segments = Route.matches route segments

let parse (segments: string list) =
    match segments with
    | At Routes.home [] -> Page.Home
    | At Routes.journey [] -> Page.Journey
    | At Routes.trip [ token ] -> Page.Trip token
    | At Routes.signInPage [] -> Page.SignIn
    | At Routes.myTripsPage [] -> Page.MyTrips
    | At Routes.accountPage [] -> Page.Account
    | At Routes.privacyPage [] -> Page.Reading ReadingPage.Privacy
    | At Routes.termsPage [] -> Page.Reading ReadingPage.Terms
    | At Routes.contactPage [] -> Page.Reading ReadingPage.Contact
    | At Routes.creditsPage [] -> Page.Credits
    | At Routes.myTripPage [ id ] -> Page.MyTrip id
    | At Routes.editTripPage [ id ] -> Page.EditTrip id
    | At Routes.explorePage [] -> Page.Browse Browse.Places
    | At Routes.regionPage [ slug ] -> Page.Browse(Browse.Region slug)
    | At Routes.foodPage [] -> Page.Browse Browse.Food
    | At Routes.foodRegionPage [ slug ] -> Page.Browse(Browse.FoodRegion slug)
    | At Routes.feelingPage [ slug ] -> Page.Browse(Browse.Feeling slug)
    | At Routes.dishPage [ slug ] -> Page.Browse(Browse.Dish slug)
    | _ -> Page.NotFound
