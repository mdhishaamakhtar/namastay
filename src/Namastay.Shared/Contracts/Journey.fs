namespace Namastay.Shared.Contracts

type JourneyBranch = {
    OnAnswer: bool
    ToCard: string
    Priority: int
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
    Colours: PlaceColours option
    Branches: JourneyBranch list
}

/// The whole graph, so swiping never waits on the network.
type JourneyGraph = { Cards: JourneyCard list }

type SavedTrip = { ShareToken: string; Trip: Trip }
