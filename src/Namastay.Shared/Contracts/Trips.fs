namespace Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type TravelMode =
    | Flight
    | Train
    | Road
    | Ferry
    | CableCar

[<RequireQualifiedAccess>]
type TransportComfort =
    | Anything
    | NoFlights

type CardAnswer = { Card: string; Liked: bool }

/// What a trip is limited to, when it's planned from a region, feeling or dish.
[<RequireQualifiedAccess>]
type ScopeKind =
    | Region
    | FoodInRegion
    | Feeling
    | Dish

/// A trip can be planned around several feelings at once. Every other kind names one thing.
type TripScope = { Kind: ScopeKind; Slugs: string list }

type TripRequest = {
    Answers: CardAnswer list
    Days: int
    Month: int option
    Travellers: TravellerType
    Budget: BudgetLevel
    TransportComfort: TransportComfort
    Scope: TripScope option
    Diet: Diet option
}

type TravelLeg = {
    Mode: TravelMode
    MinutesMin: int
    MinutesMax: int
    ComfortNote: string option
}

type TripDay = { Day: int; Plan: string }

type TripDish = {
    Name: string
    Description: string
    Kind: DishKind
}

type TripEatery = {
    Name: string
    Area: string
    VegetarianOnly: bool
}

/// What to try at a stop. Trips saved before food existed have none.
type StopFood = {
    Dishes: TripDish list
    Eateries: TripEatery list
}

type TripStop = {
    PlaceSlug: string
    Name: string
    /// Why the stop is in the trip, in one line. Trips saved before reasons existed have none.
    Reason: string option
    Days: int
    FamilySlug: string
    Colours: PlaceColours
    Access: PlaceAccess
    StayArea: string option
    Photo: GuidePhoto option
    Plans: TripDay list
    Food: StopFood option
    Arrival: TravelLeg option
    Retired: bool
    Custom: bool
}

type TripSwap = {
    From: string
    To: string
    Reason: string
}

type TripNotice = {
    PlaceSlug: string option
    Kind: GuideNoticeKind
    Severity: NoticeSeverity
    Body: string
}

type MixShare = { Name: string; Share: int }

/// Where a visitor lands or leaves from, and the journey to or from their first or last stop.
type TripGateway = {
    City: string
    Airport: string
    Via: string option
    Leg: TravelLeg option
}

type Trip = {
    Title: string
    BaseFamily: string
    Arrive: TripGateway option
    Depart: TripGateway option
    Stops: TripStop list
    Swaps: TripSwap list
    Mix: MixShare list
    LeftOut: string list
    Notices: TripNotice list
    Month: int option
    BestMonths: int list
    Budget: BudgetLevel
    Diet: Diet option
    DataVersion: int
}

type StopEdit = {
    PlaceSlug: string option
    CustomName: string option
    Days: int
}

type TripEdit = {
    Title: string
    Month: int option
    Stops: StopEdit list
}

type EditedTrip = { Trip: Trip; Warnings: string list }
