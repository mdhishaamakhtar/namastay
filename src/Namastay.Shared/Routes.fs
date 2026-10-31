module Namastay.Shared.Routes

open Namastay.Shared.Contracts

let searchPlaces: Route<NoBody, PlaceSummary list> = Route.get "/api/places"
let placeGuide: Route<NoBody, PlaceGuide> = Route.get "/api/places/{slug}"
