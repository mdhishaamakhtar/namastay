namespace Namastay.Domain.Catalog

[<RequireQualifiedAccess>]
type IllustrationStatus =
    | Brief
    | Draft
    | Final

type Illustration = {
    Slug: string
    FilePath: string
    Alt: string
    Brief: string
    Status: IllustrationStatus
}

type Photo = {
    Slug: string
    PlaceSlug: string
    FilePath: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
    Sort: int
}

type CardBranch = {
    OnAnswer: bool
    ToCard: string
    Priority: int
}

type CardSignal = { Signal: string; Weight: int }

type Card = {
    Slug: string
    PlaceSlug: string option
    Title: string
    Line: string
    PhotoSlug: string option
    IllustrationSlug: string option
    IsStart: bool
    Branches: CardBranch list
    Signals: CardSignal list
}
