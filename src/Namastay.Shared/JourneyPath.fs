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
        | _ -> Some "Good picks. Your trip is nearly drawn."

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

/// The feeling itself, or one of its children: quiet beaches speak for beaches.
let speaksFor (interests: JourneyInterest list) (feeling: string) (card: JourneyCard) =
    card.Interests
    |> List.exists (fun item ->
        item.Slug = feeling
        || interests
           |> List.exists (fun interest -> interest.Slug = item.Slug && interest.Parent = Some feeling))

/// How far ahead a card's feeling is of the feeling shown least, in a deck planned from
/// several. A card for the feeling that's behind, or for none of them, is not ahead at all.
let private ahead (graph: JourneyGraph) (feelings: string list) (shown: JourneyCard list) (card: JourneyCard) =
    let seen (feeling: string) =
        shown |> List.filter (speaksFor graph.Interests feeling) |> List.length

    match feelings |> List.filter (fun feeling -> speaksFor graph.Interests feeling card) with
    | [] -> 0
    | spoken -> (spoken |> List.map seen |> List.min) - (feelings |> List.map seen |> List.min)

/// What each card a feeling is ahead by costs, while the deck opens and once it leans.
let private openingSpread = 20
let private leaningSpread = 10

/// Rewards whatever the visitor hasn't seen yet: another region, or, in a deck planned
/// from several feelings, whichever of them has been shown least.
let private freshness (graph: JourneyGraph) (feelings: string list) (shown: JourneyCard list) (card: JourneyCard) =
    let start = if card.IsStart then startBonus else 0

    let spread =
        match feelings with
        | _ :: _ :: _ -> openingSpread * ahead graph feelings shown card
        | _ -> openingSpread * countOf card.Region _.Region shown

    start
    - spread
    - 10 * countOf card.Interest _.Interest shown
    - 30 * countOf card.Place _.Place shown

/// What the same interest is worth between two cards, and what a parent or child is.
let private sameKin = 2
let private closeKin = 1

/// How closely two interests are related: the same, or parent and child.
let private kinship (interests: JourneyInterest list) (one: string) (other: string) =
    if one = other then
        sameKin
    elif
        interests
        |> List.exists (fun interest ->
            (interest.Slug = one && interest.Parent = Some other)
            || (interest.Slug = other && interest.Parent = Some one))
    then
        closeKin
    else
        0

/// How much a card's strongest interest has in common with everything another card speaks for.
let private shared (interests: JourneyInterest list) (card: JourneyCard) (other: JourneyCard) =
    card.Interests
    |> List.truncate 1
    |> List.sumBy (fun mine ->
        other.Interests
        |> List.sumBy (fun theirs -> kinship interests mine.Slug theirs.Slug * min mine.Weight theirs.Weight))

/// Rewards what they've liked and steers away from what they've passed, without
/// letting one thing take over. A pass on a place counts far more than a like.
let private leaning (graph: JourneyGraph) (answers: CardAnswer list) (shown: JourneyCard list) (card: JourneyCard) =
    let liked = likedOf graph answers
    let passed = shown |> List.except liked
    let recent = shown |> List.rev |> List.truncate 2
    let inCommon = shared graph.Interests card

    let followsLike =
        liked
        |> List.exists (fun item ->
            item.Branches
            |> List.exists (fun branch -> branch.OnAnswer && branch.ToCard = card.Slug))

    (if followsLike then 12 else 0)
    + List.sumBy inCommon liked
    + 3 * countOf card.Region _.Region liked
    - 2 * List.sumBy inCommon passed / 3
    - 3 * countOf card.Region _.Region passed
    - 2 * List.sumBy inCommon recent / 3
    - 4 * countOf card.Place _.Place liked
    - 15 * countOf card.Place _.Place passed

let private isWildcard (answered: int) =
    answered % wildcardEvery = wildcardEvery - 1

/// The next card. The first few show how varied India is, then the deck leans towards
/// what the visitor likes, and every so often it offers something different.
/// The seed is the visit's own, so two visitors don't open with the same cards.
/// `feelings` are the ones the trip is planned from. With several, the deck keeps them level.
let next (seed: int) (feelings: string list) (graph: JourneyGraph) (answers: CardAnswer list) =
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
            freshness graph feelings shown card + shuffle seed openingShuffle card.Slug
        else
            leaning graph answers shown card
            - leaningSpread * ahead graph feelings shown card
            + shuffle seed leaningShuffle card.Slug

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

/// The liked interests, most liked first. Ties go to the one liked first.
let private ranked (graph: JourneyGraph) (answers: CardAnswer list) =
    likedOf graph answers
    |> List.choose _.Interest
    |> List.countBy id
    |> List.sortBy (fun (_, likes) -> -likes)

/// The strongest interests so far, each as a percentage of the strongest.
let mix (graph: JourneyGraph) (answers: CardAnswer list) =
    let leading = ranked graph answers |> List.truncate mixSize

    match leading with
    | [] -> []
    | (_, top) :: _ -> leading |> List.map (fun (name, likes) -> name, likes * 100 / top)

/// The leading interests must hold for this many swipes before the picture counts as clear.
let settledAfter = 8
let private leaders = 2

/// The leaders must make up this share of everything liked: two thirds.
let private leadersShare = 2, 3

/// Whether the visitor's leading interests have stopped changing, so more cards would
/// sharpen the trip without changing what it's about. The same two must have led after
/// each of the last few swipes, with enough likes to count and most of the likes between
/// them, so someone who likes a little of everything is never told the picture is clear.
let settled (graph: JourneyGraph) (answers: CardAnswer list) =
    let leadingAfter (count: int) =
        ranked graph (List.truncate count answers)
        |> List.truncate leaders
        |> List.map fst
        |> Set.ofList

    let all = ranked graph answers
    let leading = all |> List.truncate leaders |> List.sumBy snd
    let part, whole = leadersShare

    match all with
    | (_, likes) :: _ when
        likes >= likesToLead
        && answers.Length > settledAfter
        && leading * whole >= List.sumBy snd all * part
        ->
        [ answers.Length - settledAfter .. answers.Length - 1 ]
        |> List.forall (fun count -> leadingAfter count = leadingAfter answers.Length)
    | _ -> false

/// What the journey says in place of the cheer once the swipes have settled.
let offer = "We've got a clear picture. Build it, or keep swiping."
