namespace Namastay.Shared.Contracts

type JourneyBranch = {
    OnAnswer: bool
    ToCard: string
    Priority: int
}

/// An interest a card speaks for, and how strongly.
type CardInterest = { Slug: string; Weight: int }

/// An interest and its parent, so quiet beaches can count towards beaches.
type JourneyInterest = {
    Slug: string
    Name: string
    Parent: string option
}

type JourneyCard = {
    Slug: string
    Title: string
    Line: string
    IsStart: bool
    ImageUrl: string option
    ImageAlt: string option
    ImageCredit: string option
    MarkUrl: string option
    /// The strongest interest, named, for the label on the card.
    Interest: string option
    Interests: CardInterest list
    Colours: PlaceColours option
    Branches: JourneyBranch list
    Place: string option
    Region: string option
}

/// The whole graph, so swiping never waits on the network.
type JourneyGraph = {
    Cards: JourneyCard list
    Interests: JourneyInterest list
}

type SavedTrip = { ShareToken: string; Trip: Trip }
