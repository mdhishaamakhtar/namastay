module Namastay.UnitTests.JourneyPathTests

open FsCheck.Xunit
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
    Interests =
        interest
        |> Option.map (fun name -> {
            Slug = name.ToLowerInvariant()
            Weight = 3
        })
        |> Option.toList
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

/// Enough cards of four interests to swipe well past the settling window.
let private tastes = {
    Cards = [
        for name, tag in [ "Mountains", "m"; "Beaches", "b"; "Faith", "f"; "Food", "d" ] do
            for index in 1..12 -> card $"{tag}{index}" false (Some name) None []
    ]
    Interests = []
}

let private likes (slugs: string list) =
    answered [ for slug in slugs -> slug, true ]

let private passes (slugs: string list) =
    answered [ for slug in slugs -> slug, false ]

let private opening = likes [ "m1"; "b1"; "m2"; "m3" ]

[<Fact>]
let ``the swipes settle once the same two interests have led for a while`` () =
    let answers =
        opening
        @ likes [ "b2"; "m4" ]
        @ passes [ "f1"; "f2" ]
        @ likes [ "b3"; "m5"; "b4"; "m6" ]

    Assert.True(JourneyPath.settled tastes answers)

[<Fact>]
let ``the two leaders trading places is still settled`` () =
    let answers = opening @ likes [ for index in 2..9 -> $"b{index}" ]
    Assert.Equal<(string * int) list>([ "Beaches", 100; "Mountains", 33 ], JourneyPath.mix tastes answers)
    Assert.True(JourneyPath.settled tastes answers)

[<Fact>]
let ``one interest on its own can settle`` () =
    let answers = likes [ for index in 1..9 -> $"m{index}" ]
    Assert.True(JourneyPath.settled tastes answers)

[<Fact>]
let ``a mix that just changed is not settled`` () =
    let steady = opening @ likes [ for index in 4..10 -> $"m{index}" ]
    let changed = steady @ likes [ "f1"; "f2" ]
    Assert.True(JourneyPath.settled tastes steady)
    Assert.False(JourneyPath.settled tastes changed)

[<Fact>]
let ``a changed mix settles again once it has held`` () =
    let changed =
        opening @ likes [ for index in 4..10 -> $"m{index}" ] @ likes [ "f1"; "f2" ]

    let held =
        changed
        @ passes [ for index in 2 .. (JourneyPath.settledAfter + 1) -> $"b{index}" ]

    Assert.True(JourneyPath.settled tastes held)

[<Fact>]
let ``the swipes are not settled one card short of the window`` () =
    let answers = likes [ for index in 1 .. JourneyPath.settledAfter -> $"m{index}" ]
    Assert.False(JourneyPath.settled tastes answers)

[<Theory>]
[<InlineData(0)>]
[<InlineData(3)>]
let ``too few answers are not enough to say`` (count: int) =
    let answers = likes [ for index in 1..count -> $"m{index}" ]
    Assert.False(JourneyPath.settled tastes answers)

[<Fact>]
let ``passing everything after one like is not a clear picture`` () =
    let answers = likes [ "m1" ] @ passes [ for index in 1..12 -> $"b{index}" ]
    Assert.False(JourneyPath.settled tastes answers)

[<Fact>]
let ``liking a little of everything is not a clear picture`` () =
    let waited = passes [ for index in 3..12 -> $"d{index}" ]
    let focused = likes [ "m1"; "m2"; "m3"; "b1"; "b2"; "f1" ] @ waited
    let spread = likes [ "m1"; "m2"; "m3"; "b1"; "b2"; "f1"; "f2"; "d1"; "d2" ] @ waited
    Assert.True(JourneyPath.settled tastes focused)
    Assert.False(JourneyPath.settled tastes spread)

[<Fact>]
let ``nothing liked never settles`` () =
    let answers = passes [ for index in 1..12 -> $"m{index}" ]
    Assert.False(JourneyPath.settled tastes answers)

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
let rec private answerCards seed graph (decide: JourneyCard -> bool) limit answers shown =
    match JourneyPath.next seed [] graph answers with
    | Some slug when List.length answers < limit ->
        let card = graph.Cards |> List.find (fun item -> item.Slug = slug)
        answerCards seed graph decide limit (answers @ [ { Card = slug; Liked = decide card } ]) (shown @ [ card ])
    | _ -> shown

let private playSeeded seed graph decide limit =
    answerCards seed graph decide limit [] []

let private play graph decide limit = playSeeded 0 graph decide limit

[<Property>]
let ``the opening cards all come from different regions whatever the seed`` (seed: int) =
    let regions = playSeeded seed india (fun _ -> false) 6 |> List.choose _.Region
    regions |> List.distinct |> List.length = 6

[<Property>]
let ``the opening mixes interests whatever the seed`` (seed: int) =
    let interests = playSeeded seed india (fun _ -> false) 4 |> List.choose _.Interest
    interests |> List.distinct |> List.length = 2

[<Fact>]
let ``different visits open with different cards`` () =
    let openers =
        [ 1..20 ]
        |> List.choose (fun seed -> JourneyPath.next seed [] india [])
        |> List.distinct

    Assert.True(openers.Length >= 4, $"only {openers.Length} different openers")

[<Fact>]
let ``the opening is drawn from the start cards`` () =
    let starts = {
        india with
            Cards =
                india.Cards
                |> List.map (fun item -> {
                    item with
                        IsStart = item.Slug.EndsWith "-one"
                })
    }

    for seed in 1..20 do
        let opening = playSeeded seed starts (fun _ -> false) 6
        Assert.All(opening, (fun card -> Assert.True card.IsStart))

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
                            IsStart = true
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
let ``the same seed and answers always give the same next card`` () =
    Assert.Equal(JourneyPath.next 7 [] india [], JourneyPath.next 7 [] india [])

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

let private withTree = {
    india with
        Cards =
            india.Cards
            @ [
                travelCard "coast-quiet" "coast-three" "coast" "Quiet beaches"
                travelCard "coast-party" "coast-four" "coast" "Party beaches"
                travelCard "hills-beach" "hills-three" "hills" "Beaches"
            ]
        Interests = [
            {
                Slug = "beaches"
                Name = "Beaches"
                Parent = None
            }
            {
                Slug = "quiet beaches"
                Name = "Quiet beaches"
                Parent = Some "beaches"
            }
            {
                Slug = "party beaches"
                Name = "Party beaches"
                Parent = Some "beaches"
            }
        ]
}

[<Fact>]
let ``a liked parent interest pulls in its children`` () =
    let likes (card: JourneyCard) = card.Slug = "hills-beach"
    let shown = playSeeded 3 withTree likes 16 |> List.map _.Slug
    let quiet = List.findIndex ((=) "coast-quiet") shown
    let party = List.findIndex ((=) "coast-party") shown
    Assert.True(max quiet party < 12, $"children came at {quiet} and {party}")

[<Fact>]
let ``passing a place twice keeps its other cards away`` () =
    let agra = {
        india with
            Cards =
                india.Cards
                @ [ for index in 1..3 -> travelCard $"taj-{index}" "agra" "north" "Faith" ]
    }

    let passesAgra (card: JourneyCard) = card.Place <> Some "agra"

    let shown =
        playSeeded 5 agra passesAgra 20
        |> List.filter (fun card -> card.Place = Some "agra")

    Assert.True(shown.Length <= 2, $"agra was shown {shown.Length} times")

/// Two feelings, each with plenty of cards, and one region holding nearly all of the first.
let private twoFeelings = {
    Cards = [
        for index in 1..12 do
            travelCard $"temple-{index}" $"temple-town-{index}" "south" "Faith"

        for index in 1..12 do
            travelCard $"beach-{index}" $"beach-town-{index}" $"coast-{index % 4}" "Beaches"
    ]
    Interests = []
}

let private planned = [ "faith"; "beaches" ]

[<TailCall>]
let rec private swipeAll seed feelings graph limit (answers: CardAnswer list) =
    match JourneyPath.next seed feelings graph answers with
    | Some slug when answers.Length < limit ->
        swipeAll seed feelings graph limit (answers @ [ { Card = slug; Liked = true } ])
    | _ -> answers |> List.map _.Card

let private countOf (prefix: string) (slugs: string list) =
    slugs |> List.filter _.StartsWith(prefix) |> List.length

[<Theory>]
[<InlineData(1)>]
[<InlineData(7)>]
[<InlineData(42)>]
let ``a deck planned from two feelings opens level between them`` (seed: int) =
    let opening = swipeAll seed planned twoFeelings 6 []
    Assert.Equal(3, countOf "temple-" opening)
    Assert.Equal(3, countOf "beach-" opening)

[<Theory>]
[<InlineData(1)>]
[<InlineData(7)>]
[<InlineData(42)>]
let ``a deck planned from two feelings keeps showing both once it leans`` (seed: int) =
    let shown = swipeAll seed planned twoFeelings 20 []
    let temples, beaches = countOf "temple-" shown, countOf "beach-" shown
    Assert.True(min temples beaches >= 8, $"{temples} temples and {beaches} beaches")

[<Fact>]
let ``one feeling spreads the opening across regions, as the open deck does`` () =
    Assert.Equal<string list>(swipeAll 7 [] twoFeelings 6 [], swipeAll 7 [ "faith" ] twoFeelings 6 [])

[<Fact>]
let ``a card speaks for its own interest and for the parent of it`` () =
    let quiet = travelCard "cove" "cove" "coast" "Quiet beaches"
    Assert.True(JourneyPath.speaksFor withTree.Interests "beaches" quiet)
    Assert.True(JourneyPath.speaksFor withTree.Interests "quiet beaches" quiet)
    Assert.False(JourneyPath.speaksFor withTree.Interests "party beaches" quiet)
