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
    Colours = deep |> Option.map colours
    Branches = branches
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
}

let answered (slugs: (string * bool) list) =
    slugs |> List.map (fun (slug, liked) -> { Card = slug; Liked = liked })

[<Fact>]
let ``journey starts with the start cards`` () =
    Assert.Equal<string list>([ "a"; "b" ], JourneyPath.start graph)

[<Fact>]
let ``a like brings its branches next, highest priority first`` () =
    Assert.Equal<string list>([ "d"; "c"; "b" ], JourneyPath.advance graph [ "b" ] "a" true [ "a" ])

[<Fact>]
let ``a pass skips like branches`` () =
    Assert.Equal<string list>([ "b" ], JourneyPath.advance graph [ "b" ] "a" false [ "a" ])

[<Fact>]
let ``a card seen before never returns`` () =
    Assert.Equal<string list>([ "b" ], JourneyPath.advance graph [ "b" ] "a" true [ "a"; "c"; "d" ])

[<Fact>]
let ``unknown branch target is ignored`` () =
    let broken = {
        Cards = [ card "a" true None None [ onLike "ghost" 1 ] ]
    }

    Assert.Empty(JourneyPath.advance broken [] "a" true [ "a" ])

[<Fact>]
let ``journey ends at the card limit`` () =
    Assert.True(JourneyPath.isFinished [ "more" ] JourneyPath.maxCards)
    Assert.False(JourneyPath.isFinished [ "more" ] (JourneyPath.maxCards - 1))

[<Fact>]
let ``journey ends when cards run out`` () =
    Assert.True(JourneyPath.isFinished [] 3)

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
    }

    let answers = answered [ for name in [ "A"; "B"; "C"; "D" ] -> name, true ]
    Assert.Equal(3, (JourneyPath.mix world answers).Length)

[<Fact>]
let ``mix is empty before any like`` () = Assert.Empty(JourneyPath.mix graph [])
