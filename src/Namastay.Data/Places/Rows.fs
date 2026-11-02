module Namastay.Data.Places.Rows

/// Mutable rows for Dapper to fill. Contracts stay plain immutable records.

[<CLIMutable>]
type SummaryRow = {
    Slug: string
    Name: string
    Region: string
    FamilyDeep: string
    Published: bool
}

[<CLIMutable>]
type HeadRow = {
    Slug: string
    Name: string
    State: string
    Region: string
    Summary: string
    IsThisForYou: string option
    GettingThere: string option
    FoodIntro: string option
    MarkPath: string option
    DaysMin: int
    DaysMax: int
    CostLow: int option
    CostHigh: int option
    Gate: string
    Deep: string
    Mid: string
    Pale: string
    TextOnDeep: string
    LastChecked: string
}

[<CLIMutable>]
type MonthRow = {
    Month: int
    Rating: string
    Note: string option
}

[<CLIMutable>]
type ExperienceRow = {
    Name: string
    Description: string
    Hedged: bool
}

[<CLIMutable>]
type DayRow = {
    Variant: string
    Day: int
    Plan: string
}

[<CLIMutable>]
type StayRow = {
    Name: string
    CostLevel: string
    Note: string option
    Hedged: bool
}

[<CLIMutable>]
type NoticeRow = {
    Kind: string
    Severity: string
    Body: string
    Hedged: bool
}

[<CLIMutable>]
type PhotoRow = {
    FilePath: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
}

[<CLIMutable>]
type DishRow = {
    Name: string
    Description: string
    Vegetarian: bool
    Vegan: bool
}

[<CLIMutable>]
type EateryRow = {
    Name: string
    Area: string
    GoFor: string
    CostLevel: string
    VegetarianOnly: bool
}
