module Namastay.Shared.Routes

open Namastay.Shared.Contracts

let health: Route<NoBody, HealthStatus> = Route.get "/health"
let searchPlaces: Route<NoBody, PlaceSummary list> = Route.get "/api/places"
let placeGuide: Route<NoBody, PlaceGuide> = Route.get "/api/places/{slug}"
let journeyStart: Route<NoBody, JourneyGraph> = Route.get "/api/journey/start"
let createItinerary: Route<TripRequest, SavedTrip> = Route.post "/api/itineraries"

let getItinerary: Route<NoBody, SavedTrip> =
    Route.get "/api/itineraries/{shareToken}"
