namespace Namastay.Shared.Contracts

type PlaceColours = {
    Deep: string
    Mid: string
    Pale: string
    TextOnDeep: string
}

/// Search result sent to the client. Only Fable-compatible types belong in this project.
type PlaceSummary = {
    Slug: string
    Name: string
    Region: string
    FamilyDeep: string
    Published: bool
}

[<RequireQualifiedAccess>]
type MonthRating =
    | Best
    | Ok
    | Avoid
    | Closed
    | Unknown

type GuideMonth = {
    Month: int
    Rating: MonthRating
    Note: string option
}

/// `Hedged` means the copy must say the fact rests on one source or sources conflict.
type GuideExperience = {
    Name: string
    Description: string
    Hedged: bool
}

type GuideDay = {
    Variant: string
    Day: int
    Plan: string
}

type GuideStay = {
    Name: string
    CostLevel: string
    Note: string option
    Hedged: bool
}

type GuideNotice = {
    Kind: string
    Severity: string
    Body: string
    Hedged: bool
}

type GuidePhoto = {
    Url: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
}

type PlaceGuide = {
    Slug: string
    Name: string
    State: string
    Region: string
    Summary: string
    IsThisForYou: string option
    DaysMin: int
    DaysMax: int
    CostLow: int option
    CostHigh: int option
    Gate: string
    Colours: PlaceColours
    Suits: string list
    Months: GuideMonth list
    Experiences: GuideExperience list
    DayPlans: GuideDay list
    StayAreas: GuideStay list
    Notices: GuideNotice list
    Photos: GuidePhoto list
    LastChecked: string
}
