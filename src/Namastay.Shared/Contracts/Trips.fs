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
    Plans: TripDay list
    Arrival: TravelLeg option
    Retired: bool
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

type Trip = {
    Title: string
    BaseFamily: string
    Stops: TripStop list
    Swaps: TripSwap list
    LeftOut: string list
    Notices: TripNotice list
    BestMonths: int list
    Budget: BudgetLevel
    DataVersion: int
}
