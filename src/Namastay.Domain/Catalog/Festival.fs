namespace Namastay.Domain.Catalog

open System

type FestivalDate = {
    Year: int
    Starts: DateOnly
    Ends: DateOnly
    IsMoonDependent: bool
}

type Festival = {
    Slug: string
    Name: string
    PlaceSlug: string option
    DateRule: string
    VisitorNotes: string
    Dates: FestivalDate list
    Evidence: Evidence
}
