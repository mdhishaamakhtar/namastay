namespace Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type PlaceAccess =
    | Open
    | Warning
    | Permit

[<RequireQualifiedAccess>]
type GuideNoticeKind =
    | Closure
    | Safety
    | Legal
    | Permit

[<RequireQualifiedAccess>]
type NoticeSeverity =
    | Info
    | Warning
    | Permit
    | Danger

[<RequireQualifiedAccess>]
type TravellerType =
    | Solo
    | Couple
    | Friends
    | Family

[<RequireQualifiedAccess>]
type BudgetLevel =
    | Budget
    | Mid
    | Luxury

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
    CostLevel: BudgetLevel
    Note: string option
    Hedged: bool
}

type GuideNotice = {
    Kind: GuideNoticeKind
    Severity: NoticeSeverity
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
    GettingThere: string option
    MarkUrl: string option
    DaysMin: int
    DaysMax: int
    CostLow: int option
    CostHigh: int option
    Access: PlaceAccess
    Colours: PlaceColours
    Suits: TravellerType list
    Months: GuideMonth list
    Experiences: GuideExperience list
    DayPlans: GuideDay list
    StayAreas: GuideStay list
    Notices: GuideNotice list
    Photos: GuidePhoto list
    LastChecked: string
}
