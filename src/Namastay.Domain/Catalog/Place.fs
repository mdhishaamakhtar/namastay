namespace Namastay.Domain.Catalog

[<RequireQualifiedAccess>]
type Gate =
    | Open
    | Warn
    | Permit

[<RequireQualifiedAccess>]
type Popularity =
    | Mainstream
    | Between
    | Offbeat

[<RequireQualifiedAccess>]
type Rating =
    | Best
    | Ok
    | Avoid
    | Closed
    | Unknown

[<RequireQualifiedAccess>]
type Traveller =
    | Solo
    | Couple
    | Friends
    | Family

[<RequireQualifiedAccess>]
type CostLevel =
    | Budget
    | Mid
    | Luxury

type PlaceInterest = { InterestSlug: string; Weight: int }

type PlaceMonth = {
    Month: int
    Rating: Rating
    Note: string option
    Evidence: Evidence
}

type TravellerFit = { Traveller: Traveller; Fit: int }

type Experience = {
    Slug: string
    Name: string
    Description: string
    Sort: int
    Evidence: Evidence
}

type DayPlan = {
    Variant: string
    Day: int
    Plan: string
    Evidence: Evidence
}

type StayArea = {
    Name: string
    CostLevel: CostLevel
    Note: string option
    Evidence: Evidence
}

type Place = {
    Slug: string
    ClusterSlug: string
    Name: string
    State: string
    FamilySlug: string
    Shade: Shade
    Gate: Gate
    Popularity: Popularity
    DaysMin: int
    DaysMax: int
    CostLow: int option
    CostHigh: int option
    Summary: string
    IsThisForYou: string option
    GettingThere: string option
    Published: bool
    Interests: PlaceInterest list
    Months: PlaceMonth list
    TravellerFit: TravellerFit list
    Experiences: Experience list
    DayPlans: DayPlan list
    StayAreas: StayArea list
    Evidence: Evidence
}
