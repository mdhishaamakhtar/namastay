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
    | Season

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

type Feeling = {
    Slug: string
    Name: string
    Places: string list
    Colour: string
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

type GuideDish = {
    Name: string
    Description: string
    Vegetarian: bool
}

type GuideEatery = {
    Name: string
    Area: string
    GoFor: string
    Cost: string
    VegetarianOnly: bool
}

type GuidePhoto = {
    Url: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
}

/// A card chosen for the landing page, shown by what it's a photo of.
type LandingCard = {
    PlaceSlug: string
    Place: string
    Title: string
    Colour: string
    Food: bool
    Photo: GuidePhoto
}

type LandingPage = {
    PlaceCount: int
    Cards: LandingCard list
}

type DirectoryRegion = {
    Slug: string
    Name: string
    Colour: string
    Places: PlaceSummary list
}

type FoodDish = {
    Slug: string
    Name: string
    Place: string
    PlaceSlug: string
}

type FoodRegion = {
    Slug: string
    Name: string
    Colour: string
    Dishes: FoodDish list
}

type FoodPage = {
    Cards: LandingCard list
    Regions: FoodRegion list
}

/// One line in a region, feeling or dish page: a place or a dish with why it's there.
type Listing = {
    Slug: string
    Name: string
    Line: string
    Colour: string
}

type BrowsePage = {
    Slug: string
    Name: string
    Colour: string
    Items: Listing list
}

type DishPage = {
    Slug: string
    Name: string
    Description: string
    Photo: GuidePhoto option
    Places: Listing list
}

/// Who took a photo, for the credits page.
type PhotoCredit = {
    Place: string
    Subject: string
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
    FoodIntro: string option
    Dishes: GuideDish list
    Eateries: GuideEatery list
    DayPlans: GuideDay list
    StayAreas: GuideStay list
    Notices: GuideNotice list
    Photos: GuidePhoto list
    LastChecked: string
}
