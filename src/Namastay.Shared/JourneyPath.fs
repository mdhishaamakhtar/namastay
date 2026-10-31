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

let start (graph: JourneyGraph) =
    graph.Cards |> List.filter _.IsStart |> List.map _.Slug

/// The cards still to come after answering one. Branches go first, so the journey follows the answer.
let advance (graph: JourneyGraph) (queue: string list) (slug: string) (liked: bool) (seen: string list) =
    let branches =
        find graph slug
        |> Option.map _.Branches
        |> Option.defaultValue []
        |> List.filter (fun branch -> branch.OnAnswer = liked)
        |> List.sortBy (fun branch -> -branch.Priority)
        |> List.map _.ToCard
        |> List.filter (fun target -> (find graph target).IsSome && not (List.contains target seen))

    (branches @ queue)
    |> List.filter (fun target -> not (List.contains target seen))
    |> List.distinct

let isFinished (queue: string list) (answered: int) = queue.IsEmpty || answered >= maxCards

let private likedCards (graph: JourneyGraph) (answers: CardAnswer list) =
    answers
    |> List.filter _.Liked
    |> List.choose (fun answer -> find graph answer.Card)

/// The colours the stage should take, or None to stay on brand.
let leadingColours (graph: JourneyGraph) (answers: CardAnswer list) =
    let ranked =
        likedCards graph answers
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
        likedCards graph answers
        |> List.choose _.Interest
        |> List.countBy id
        |> List.sortBy (fun (_, likes) -> -likes)
        |> List.truncate mixSize

    match ranked with
    | [] -> []
    | (_, top) :: _ -> ranked |> List.map (fun (name, likes) -> name, likes * 100 / top)
