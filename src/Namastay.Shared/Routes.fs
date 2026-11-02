module Namastay.Shared.Routes

open Namastay.Shared.Contracts

let home: Route<NoBody, string> = Route.get "/"
let journey: Route<NoBody, string> = Route.get "/journey"
let trip: Route<NoBody, string> = Route.get "/trip/{shareToken}"
let signInPage: Route<NoBody, string> = Route.get "/sign-in"
let myTripsPage: Route<NoBody, string> = Route.get "/my"
let accountPage: Route<NoBody, string> = Route.get "/account"
let privacyPage: Route<NoBody, string> = Route.get "/privacy"
let termsPage: Route<NoBody, string> = Route.get "/terms"
let contactPage: Route<NoBody, string> = Route.get "/contact"
let creditsPage: Route<NoBody, string> = Route.get "/credits"
let myTripPage: Route<NoBody, string> = Route.get "/my/{id}"
let editTripPage: Route<NoBody, string> = Route.get "/my/{id}/edit"
let guidePage: Route<NoBody, string> = Route.get "/places/{slug}"
let explorePage: Route<NoBody, string> = Route.get "/explore"
let foodPage: Route<NoBody, string> = Route.get "/food"
let regionPage: Route<NoBody, string> = Route.get "/explore/{slug}"
let foodRegionPage: Route<NoBody, string> = Route.get "/food/{slug}"
let feelingPage: Route<NoBody, string> = Route.get "/feelings/{slug}"
let dishPage: Route<NoBody, string> = Route.get "/dishes/{slug}"
let sitemap: Route<NoBody, string> = Route.get "/sitemap.xml"
let mediaFile: Route<NoBody, string> = Route.get "/media/{**key}"

/// The address of a stored image or illustration.
let mediaUrl (path: string) = "/media/" + path

let ready: Route<NoBody, HealthStatus> = Route.get "/ready"
let health: Route<NoBody, HealthStatus> = Route.get "/health"
let searchPlaces: Route<NoBody, PlaceSummary list> = Route.get "/api/places"
let placeGuide: Route<NoBody, PlaceGuide> = Route.get "/api/places/{slug}"
let feelings: Route<NoBody, Feeling list> = Route.get "/api/feelings"
let landing: Route<NoBody, LandingPage> = Route.get "/api/landing"
let directory: Route<NoBody, DirectoryRegion list> = Route.get "/api/directory"
let food: Route<NoBody, FoodPage> = Route.get "/api/food"
let credits: Route<NoBody, PhotoCredit list> = Route.get "/api/credits"
let regionDetail: Route<NoBody, BrowsePage> = Route.get "/api/regions/{slug}"

let foodRegionDetail: Route<NoBody, BrowsePage> =
    Route.get "/api/food/regions/{slug}"

let feelingDetail: Route<NoBody, BrowsePage> = Route.get "/api/feelings/{slug}/page"
let dishDetail: Route<NoBody, DishPage> = Route.get "/api/dishes/{slug}"

let feelingPlaces: Route<NoBody, PlaceSummary list> =
    Route.get "/api/feelings/{slug}/places"

let journeyStart: Route<NoBody, JourneyGraph> = Route.get "/api/journey/start"
let createItinerary: Route<TripRequest, SavedTrip> = Route.post "/api/itineraries"

let getItinerary: Route<NoBody, SavedTrip> =
    Route.get "/api/itineraries/{shareToken}"

let signUp: Route<Credentials, Account> = Route.post "/api/auth/sign-up"
let signIn: Route<Credentials, Account> = Route.post "/api/auth/sign-in"
let issueToken: Route<Credentials, TokenGrant> = Route.post "/api/auth/token"
let setPassword: Route<NewPassword, Account> = Route.post "/api/auth/password"
let signOut: Route<NoBody, NoBody> = Route.post "/api/auth/sign-out"
let me: Route<NoBody, Account> = Route.get "/api/me"
let googleSignIn: Route<NoBody, string> = Route.get "/auth/google"
let signInOptions: Route<NoBody, SignInOptions> = Route.get "/api/auth/options"

let saveItinerary: Route<NoBody, SavedTripSummary> =
    Route.post "/api/itineraries/{shareToken}/save"

let myItineraries: Route<NoBody, SavedTripSummary list> =
    Route.get "/api/my/itineraries"

let getMine: Route<NoBody, SavedTrip> = Route.get "/api/my/itineraries/{id}"

let editMine: Route<TripEdit, EditedTrip> = Route.put "/api/my/itineraries/{id}"
let deleteMine: Route<NoBody, NoBody> = Route.delete "/api/my/itineraries/{id}"

/// Personal answers must never be kept, so a new route is personal until it's listed here.
let isPublicAnswer (url: string) =
    let path = (url.Split '?')[0]

    let isUnder (route: Route<NoBody, 'Response>) =
        let prefix = (route.Template.Split '{')[0]
        path = prefix.TrimEnd '/' || path.StartsWith prefix

    isUnder searchPlaces
    || isUnder feelings
    || isUnder landing
    || isUnder directory
    || isUnder food
    || isUnder credits
    || isUnder regionDetail
    || isUnder dishDetail
    || isUnder journeyStart
    || path = signInOptions.Template
