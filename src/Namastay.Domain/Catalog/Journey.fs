namespace Namastay.Domain.Catalog

type Illustration = {
    Slug: string
    FilePath: string
    Alt: string
    Brief: string
    Status: string
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
    IllustrationSlug: string option
    IsStart: bool
    Branches: CardBranch list
    Signals: CardSignal list
}
