module Namastay.UnitTests.JourneyPathTests

open Xunit
open Namastay.Shared
open Namastay.Shared.Contracts

let colours deep = {
    Deep = deep
    Mid = deep
    Pale = deep
    TextOnDeep = "#FFFFFF"
}

let card slug start interest deep branches = {
    Slug = slug
    Title = slug
    Line = slug
    IsStart = start
    ImageUrl = None
    ImageAlt = None
    ImageCredit = None
    MarkUrl = None
    Interest = interest
    Interests = []
    Colours = deep |> Option.map colours
    Branches = branches
    Place = Some slug
    Region = Some slug
}

let onLike target priority = {
    OnAnswer = true
    ToCard = target
    Priority = priority
}

let graph = {
    Cards = [
        card "a" true (Some "Mountains") (Some "#111") [ onLike "c" 1; onLike "d" 2 ]
        card "b" true (Some "Beaches") (Some "#222") []
        card "c" false (Some "Mountains") (Some "#111") []
        card "d" false (Some "Mountains") (Some "#111") []
        card "e" false (Some "Faith") (Some "#333") []
    ]
    Interests = []
}

let answered (slugs: (string * bool) list) =
    slugs |> List.map (fun (slug, liked) -> { Card = slug; Liked = liked })

[<Fact>]
let ``stage stays on brand until a colour has enough likes`` () =
    let answers = answered [ "a", true; "c", true ]
    Assert.Equal(None, JourneyPath.leadingColours graph answers)

[<Fact>]
let ``stage takes the colour of a clear lead`` () =
    let answers = answered [ "a", true; "c", true; "d", true ]
    Assert.Equal(Some(colours "#111"), JourneyPath.leadingColours graph answers)

[<Fact>]
let ``a level race keeps the brand colour`` () =
    let world = {
        Cards = [
            for index in 1..6 -> card $"c{index}" false None (Some(if index % 2 = 0 then "#111" else "#222")) []
        ]
        Interests = []
    }

    let answers = answered [ for index in 1..6 -> $"c{index}", true ]
    Assert.Equal(None, JourneyPath.leadingColours world answers)

[<Fact>]
let ``passes do not count towards a lead`` () =
    let answers = answered [ "a", false; "c", false; "d", false ]
    Assert.Equal(None, JourneyPath.leadingColours graph answers)

[<Fact>]
let ``a new leader takes over`` () =
    let world = {
        Cards = [
            for index in 1..3 -> card $"m{index}" false None (Some "#111") []
            for index in 1..6 -> card $"f{index}" false None (Some "#333") []
        ]
        Interests = []
    }

    let answers =
        answered [
            for slug in [ "m1"; "m2"; "m3"; "f1"; "f2"; "f3"; "f4"; "f5"; "f6" ] -> slug, true
        ]

    Assert.Equal(Some(colours "#333"), JourneyPath.leadingColours world answers)

[<Fact>]
let ``mix ranks liked interests as shares of the strongest`` () =
    let answers = answered [ "a", true; "c", true; "b", true; "d", false ]
    let mix = JourneyPath.mix graph answers
    Assert.Equal<(string * int) list>([ "Mountains", 100; "Beaches", 50 ], mix)

[<Fact>]
let ``mix keeps only the top three`` () =
    let world = {
        Cards = [ for name in [ "A"; "B"; "C"; "D" ] -> card name false (Some name) None [] ]
        Interests = []
    }

    let answers = answered [ for name in [ "A"; "B"; "C"; "D" ] -> name, true ]
    Assert.Equal(3, (JourneyPath.mix world answers).Length)

[<Fact>]
let ``mix is empty before any like`` () = Assert.Empty(JourneyPath.mix graph [])

let private travelCard slug place region interest = {
    card slug false (Some interest) None [] with
        Place = Some place
        Region = Some region
}

/// Six regions, each with cards for two interests at two places.
let private india = {
    Cards = [
        for region in [ "north"; "south"; "east"; "west"; "hills"; "coast" ] do
            for interest, tag in [ "Faith", "a"; "Food", "b" ] do
                for place in [ "one"; "two" ] do
                    travelCard $"{region}-{tag}-{place}" $"{region}-{place}" region interest
    ]
    Interests = []
}

[<TailCall>]
let rec private answerCards graph (decide: JourneyCard -> bool) limit answers shown =
    match JourneyPath.next graph answers with
    | Some slug when List.length answers < limit ->
        let card = graph.Cards |> List.find (fun item -> item.Slug = slug)
        answerCards graph decide limit (answers @ [ { Card = slug; Liked = decide card } ]) (shown @ [ card ])
    | _ -> shown

let private play graph decide limit = answerCards graph decide limit [] []

[<Fact>]
let ``the opening cards all come from different regions`` () =
    let regions = play india (fun _ -> false) 6 |> List.choose _.Region
    Assert.Equal(6, regions |> List.distinct |> List.length)

[<Fact>]
let ``the opening mixes interests`` () =
    let interests = play india (fun _ -> false) 4 |> List.choose _.Interest
    Assert.Equal(2, interests |> List.distinct |> List.length)

[<Fact>]
let ``the same place never comes twice in a row`` () =
    let places = play india (fun _ -> true) 24 |> List.choose _.Place
    Assert.All(List.pairwise places, (fun (first, second) -> Assert.NotEqual<string>(first, second)))

[<Fact>]
let ``a place appears no more than twice unless it runs out of others`` () =
    let many = {
        Cards = [
            for index in 1..6 do
                travelCard $"taj-{index}" "agra" "north" "Faith"
            for index in 1..12 do
                travelCard $"other-{index}" $"place-{index}" $"region-{index}" "Food"
        ]
        Interests = []
    }

    let shown = play many (fun _ -> true) 10 |> List.choose _.Place
    Assert.True(shown |> List.filter (fun place -> place = "agra") |> List.length <= 2)

[<Fact>]
let ``likes pull the deck towards that interest`` () =
    let later = play india (fun card -> card.Interest = Some "Food") 16 |> List.skip 6

    let food =
        later |> List.filter (fun card -> card.Interest = Some "Food") |> List.length

    Assert.True(food > later.Length / 2)

[<Fact>]
let ``variety stays after a clear lean`` () =
    let later = play india (fun card -> card.Interest = Some "Food") 16 |> List.skip 6
    Assert.Contains(later, (fun card -> card.Interest = Some "Faith"))

[<Fact>]
let ``a liked card brings its follow-up soon after the opening`` () =
    let linked = {
        Cards =
            india.Cards
            |> List.map (fun item ->
                if item.Slug = "north-a-one" then
                    {
                        item with
                            Branches = [ onLike "coast-b-two" 1 ]
                    }
                else
                    item)
        Interests = []
    }

    let answers = play linked (fun _ -> true) 9 |> List.map _.Slug
    Assert.Contains("coast-b-two", answers)

[<Fact>]
let ``a card is never shown twice`` () =
    let shown = play india (fun _ -> true) 24 |> List.map _.Slug
    Assert.Equal(shown.Length, shown |> List.distinct |> List.length)

[<Fact>]
let ``the same answers always give the same next card`` () =
    Assert.Equal(JourneyPath.next india [], JourneyPath.next india [])

[<Fact>]
let ``journey ends at the card limit`` () =
    Assert.Equal(JourneyPath.maxCards, (play india (fun _ -> true) 100).Length)

[<Fact>]
let ``journey ends when cards run out`` () =
    Assert.Equal(
        2,
        (play
            {
                india with
                    Cards = india.Cards |> List.truncate 2
            }
            (fun _ -> true)
            100)
            .Length
    )

[<Theory>]
[<InlineData(0)>]
[<InlineData(3)>]
let ``the first few cards go uncheered`` (answered: int) =
    Assert.Equal(None, JourneyPath.cheer answered)

[<Fact>]
let ``cheering starts once a few cards are done`` () =
    Assert.True((JourneyPath.cheer 4).IsSome)

[<Fact>]
let ``the cheer holds between milestones`` () =
    Assert.Equal(JourneyPath.cheer 8, JourneyPath.cheer 11)
    Assert.NotEqual(JourneyPath.cheer 8, JourneyPath.cheer 12)

[<Fact>]
let ``the last cards say how close the end is`` () =
    Assert.Equal(Some "Just a few to go.", JourneyPath.cheer (JourneyPath.maxCards - 3))
