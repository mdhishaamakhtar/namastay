module Namastay.Data.Catalog.Rows

open System

[<CLIMutable>]
type PlaceRow = {
    Slug: string
    ClusterSlug: string
    Name: string
    State: string
    FamilySlug: string
    Shade: string
    Gate: string
    Popularity: string
    DaysMin: int
    DaysMax: int
    CostLow: int option
    CostHigh: int option
    Summary: string
    Airport: string option
    Hub: bool
    Published: bool
    Status: string
    LastChecked: DateTime
}

[<CLIMutable>]
type InterestRow = {
    PlaceSlug: string
    InterestSlug: string
    Weight: int
}

[<CLIMutable>]
type MonthRow = {
    PlaceSlug: string
    Month: int
    Rating: string
    Status: string
    LastChecked: DateTime
}

[<CLIMutable>]
type FitRow = {
    PlaceSlug: string
    Traveller: string
    Fit: int
}

[<CLIMutable>]
type DayRow = {
    PlaceSlug: string
    Variant: string
    Day: int
    Plan: string
    Status: string
    LastChecked: DateTime
}

[<CLIMutable>]
type StayRow = {
    PlaceSlug: string
    Name: string
    CostLevel: string
    Status: string
    LastChecked: DateTime
}

[<CLIMutable>]
type LinkRow = {
    Slug: string
    FromSlug: string
    ToSlug: string
    Mode: string
    MinutesMin: int
    MinutesMax: int
    ComfortNote: string option
    Status: string
    LastChecked: DateTime
}

[<CLIMutable>]
type NoticeRow = {
    Slug: string
    Scope: string
    ScopeRef: string option
    Kind: string
    Severity: string
    Body: string
    Status: string
    LastChecked: DateTime
}

[<CLIMutable>]
type NamedRow = { Slug: string; Name: string }

[<CLIMutable>]
type ClusterRow = {
    Slug: string
    RegionSlug: string
    Name: string
}

[<CLIMutable>]
type FamilyRow = {
    Slug: string
    Name: string
    UsedFor: string
    Deep: string
    Mid: string
    Pale: string
    TextOnDeep: string
}

[<CLIMutable>]
type PhotoRow = {
    Slug: string
    PlaceSlug: string
    FilePath: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
    Sort: int
}

[<CLIMutable>]
type CardRow = {
    Slug: string
    PlaceSlug: string option
}

[<CLIMutable>]
type SignalRow = {
    CardSlug: string
    Signal: string
    Weight: int
}
