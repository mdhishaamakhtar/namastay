/// The rules of the swipe journey, kept pure so they can be tested without a browser.
module Namastay.Shared.JourneyPath

open Namastay.Shared.Contracts

let maxCards = 24

/// A colour needs this many likes, and this big a gap over the next, before the stage changes.
let private likesToLead = 3
let private leadToChange = 2
let private mixSize = 3

let private cheerEvery = 4
let private nearTheEnd = 4

/// Something to say once a few cards are done. It holds until the next milestone.
let cheer (answered: int) =
    if maxCards - answered <= nearTheEnd then
        Some "Just a few to go."
    else
        match answered / cheerEvery with
        | 0 -> None
        | 1 -> Some "Nice. Every swipe sharpens your trip."
        | 2 -> Some "The more you swipe, the better we can build it."
        | 3 -> Some "Halfway there. Your India is taking shape."
        | _ -> Some "Good picks. Keep going, or build it now."

let private find (graph: JourneyGraph) (slug: string) =
    graph.Cards |> List.tryFind (fun card -> card.Slug = slug)

let private likedOf (graph: JourneyGraph) (answers: CardAnswer list) =
    answers
    |> List.filter _.Liked
    |> List.choose (fun answer -> find graph answer.Card)

let private openingCards = 6
let private wildcardEvery = 4
let private mostOfOnePlace = 2

let private shownCards (graph: JourneyGraph) (answers: CardAnswer list) =
    answers |> List.choose (fun answer -> find graph answer.Card)

let private countOf (value: string option) (selector: JourneyCard -> string option) (cards: JourneyCard list) =
    cards
    |> List.filter (fun card -> value.IsSome && selector card = value)
    |> List.length

/// A start card beats any shuffle, so the opening is always drawn from the curated starts.
let private startBonus = 12
let private openingShuffle = 10
let private leaningShuffle = 5

/// Each visit's own shuffle of near ties. The same seed gives the same deck, so it can be tested.
let private shuffle (seed: int) (range: int) (slug: string) =
    let visit = (seed % 1009 + 1009) % 1009 + 1
    let letters = (slug |> Seq.sumBy int) % 1013 + 1
    visit * letters % 1021 % range

/// Rewards whatever the visitor hasn't seen yet.
let private freshness (shown: JourneyCard list) (card: JourneyCard) =
    let start = if card.IsStart then startBonus else 0

    start
    - 20 * countOf card.Region _.Region shown
    - 10 * countOf card.Interest _.Interest shown
    - 30 * countOf card.Place _.Place shown

/// Rewards what they've liked, without letting one thing take over.
let private leaning (graph: JourneyGraph) (answers: CardAnswer list) (shown: JourneyCard list) (card: JourneyCard) =
    let liked = likedOf graph answers
    let passed = shown |> List.except liked
    let recent = shown |> List.rev |> List.truncate 2

    let followsLike =
        liked
        |> List.exists (fun item ->
            item.Branches
            |> List.exists (fun branch -> branch.OnAnswer && branch.ToCard = card.Slug))

    (if followsLike then 12 else 0)
    + 6 * countOf card.Interest _.Interest liked
    + 3 * countOf card.Region _.Region liked
    - 4 * countOf card.Interest _.Interest passed
    - 2 * countOf card.Interest _.Interest recent
    - 8 * countOf card.Place _.Place shown

let private isWildcard (answered: int) =
    answered % wildcardEvery = wildcardEvery - 1

/// The next card. The first few show how varied India is, then the deck leans towards
/// what the visitor likes, and every so often it offers something different.
/// The seed is the visit's own, so two visitors don't open with the same cards.
let next (seed: int) (graph: JourneyGraph) (answers: CardAnswer list) =
    let shown = shownCards graph answers
    let seen = answers |> List.map _.Card
    let last = shown |> List.tryLast |> Option.bind _.Place

    let unseen =
        graph.Cards |> List.filter (fun card -> not (List.contains card.Slug seen))

    let allowed =
        unseen
        |> List.filter (fun card ->
            (last.IsNone || card.Place <> last)
            && countOf card.Place _.Place shown < mostOfOnePlace)

    let pool = if allowed.IsEmpty then unseen else allowed

    let score card =
        if answers.Length < openingCards || isWildcard answers.Length then
            freshness shown card + shuffle seed openingShuffle card.Slug
        else
            leaning graph answers shown card + shuffle seed leaningShuffle card.Slug

    if answers.Length >= maxCards then
        None
    else
        pool
        |> List.sortBy (fun card -> -(score card), card.Slug)
        |> List.tryHead
        |> Option.map _.Slug


/// The colours the stage should take, or None to stay on brand.
let leadingColours (graph: JourneyGraph) (answers: CardAnswer list) =
    let ranked =
        likedOf graph answers
        |> List.choose _.Colours
        |> List.countBy id
        |> List.sortBy (fun (_, likes) -> -likes)

    match ranked with
    | [ (colours, likes) ] when likes >= likesToLead -> Some colours
    | (colours, likes) :: (_, next) :: _ when likes >= likesToLead && likes - next >= leadToChange -> Some colours
    | _ -> None

/// The strongest interests so far, each as a percentage of the strongest.
let mix (graph: JourneyGraph) (answers: CardAnswer list) =
    let ranked =
        likedOf graph answers
        |> List.choose _.Interest
        |> List.countBy id
        |> List.sortBy (fun (_, likes) -> -likes)
        |> List.truncate mixSize

    match ranked with
    | [] -> []
    | (_, top) :: _ -> ranked |> List.map (fun (name, likes) -> name, likes * 100 / top)
