module Namastay.Shared.Routes

open Namastay.Shared.Contracts

let home: Route<NoBody, string> = Route.get "/"
let journey: Route<NoBody, string> = Route.get "/journey"
let trip: Route<NoBody, string> = Route.get "/trip/{shareToken}"
let signInPage: Route<NoBody, string> = Route.get "/sign-in"
let myTripsPage: Route<NoBody, string> = Route.get "/my"
let myTripPage: Route<NoBody, string> = Route.get "/my/{id}"
let editTripPage: Route<NoBody, string> = Route.get "/my/{id}/edit"
let guidePage: Route<NoBody, string> = Route.get "/places/{slug}"
let sitemap: Route<NoBody, string> = Route.get "/sitemap.xml"
let mediaFile: Route<NoBody, string> = Route.get "/media/{**key}"

/// The address of a stored image or illustration.
let mediaUrl (path: string) = "/media/" + path

let ready: Route<NoBody, HealthStatus> = Route.get "/ready"
let health: Route<NoBody, HealthStatus> = Route.get "/health"
let searchPlaces: Route<NoBody, PlaceSummary list> = Route.get "/api/places"
let placeGuide: Route<NoBody, PlaceGuide> = Route.get "/api/places/{slug}"
let feelings: Route<NoBody, Feeling list> = Route.get "/api/feelings"
let landing: Route<NoBody, LandingCard list> = Route.get "/api/landing"

let feelingPlaces: Route<NoBody, PlaceSummary list> =
    Route.get "/api/feelings/{slug}/places"

let journeyStart: Route<NoBody, JourneyGraph> = Route.get "/api/journey/start"
let createItinerary: Route<TripRequest, SavedTrip> = Route.post "/api/itineraries"

let getItinerary: Route<NoBody, SavedTrip> =
    Route.get "/api/itineraries/{shareToken}"

let signUp: Route<Credentials, Account> = Route.post "/api/auth/sign-up"
let signIn: Route<Credentials, Account> = Route.post "/api/auth/sign-in"
let signOut: Route<NoBody, NoBody> = Route.post "/api/auth/sign-out"
let me: Route<NoBody, Account> = Route.get "/api/me"
let googleSignIn: Route<NoBody, string> = Route.get "/auth/google"
let signInOptions: Route<NoBody, SignInOptions> = Route.get "/api/auth/options"

let saveItinerary: Route<NoBody, SavedTripSummary> =
    Route.post "/api/itineraries/{shareToken}/save"

let myItineraries: Route<NoBody, SavedTripSummary list> =
    Route.get "/api/my/itineraries"

let getMine: Route<NoBody, SavedTrip> = Route.get "/api/my/itineraries/{id}"

let editMine: Route<TripEdit, EditedTrip> = {
    Method = "PUT"
    Template = "/api/my/itineraries/{id}"
}

let deleteMine: Route<NoBody, NoBody> = {
    Method = "DELETE"
    Template = "/api/my/itineraries/{id}"
}
