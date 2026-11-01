/// The rules of the swipe journey, kept pure so they can be tested without a browser.
module Namastay.Shared.JourneyPath

open Namastay.Shared.Contracts

let maxCards = 24

/// A colour needs this many likes, and this big a gap over the next, before the stage changes.
let private likesToLead = 3
let private leadToChange = 2
let private mixSize = 3

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

/// A small steady nudge from the slug, so near ties don't always fall in the same order.
let private nudge (slug: string) = (slug |> Seq.sumBy int) % 5

/// Rewards whatever the visitor hasn't seen yet.
let private freshness (shown: JourneyCard list) (card: JourneyCard) =
    let start = if card.IsStart then 2 else 0

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
let next (graph: JourneyGraph) (answers: CardAnswer list) =
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
            freshness shown card + nudge card.Slug
        else
            leaning graph answers shown card + nudge card.Slug

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
