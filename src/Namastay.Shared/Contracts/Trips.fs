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

type TripRequest = {
    Answers: CardAnswer list
    Days: int
    Month: int option
    Travellers: TravellerType
    Budget: BudgetLevel
    TransportComfort: TransportComfort
}

type TravelLeg = {
    Mode: TravelMode
    MinutesMin: int
    MinutesMax: int
    ComfortNote: string option
}

type TripDay = { Day: int; Plan: string }

type TripStop = {
    PlaceSlug: string
    Name: string
    Days: int
    FamilySlug: string
    Colours: PlaceColours
    Access: PlaceAccess
    StayArea: string option
    Photo: GuidePhoto option
    Plans: TripDay list
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
