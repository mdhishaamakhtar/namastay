module Namastay.UnitTests.Bench.SessionTests

open Xunit
open Namastay.Bench
open Namastay.Bench.Personas
open Namastay.Domain.Catalog
open Namastay.Shared
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip

let private graph = ContractMapping.journeyGraph TripBuilderTests.catalog

let private persona = {
    Name = "hiker"
    Swiper = Swiper.Taste
    Interests = [ "mountains" ]
    Days = 7
    Month = None
    Travellers = TravellerType.Solo
    Budget = BudgetLevel.Mid
    Comfort = TransportComfort.Anything
    Scoped = false
    Seed = 5
}

[<Fact>]
let ``a session answers every card in a small deck and gets a trip`` () =
    let session = Session.play TripBuilderTests.catalog graph persona
    Assert.Equal(TripBuilderTests.catalog.Cards.Length, session.Answers.Length)
    Assert.Equal(session.Answers.Length, session.Shown.Length)
    Assert.True(Result.isOk session.Trip)

[<Fact>]
let ``the same traveller gets the same session`` () =
    let once = Session.play TripBuilderTests.catalog graph persona
    let again = Session.play TripBuilderTests.catalog graph persona
    Assert.Equal<CardAnswer list>(once.Answers, again.Answers)

[<Fact>]
let ``a scoped session only sees cards from the feeling's places`` () =
    let scoped = { persona with Scoped = true }
    let session = Session.play TripBuilderTests.catalog graph scoped
    Assert.NotEmpty session.Shown

    for card in session.Shown do
        Assert.Equal(Some "peak", card.Place)

[<Fact>]
let ``a deck too short to settle has no early finish`` () =
    let session = Session.play TripBuilderTests.catalog graph persona
    Assert.Equal(None, session.Early)

/// Enough mountain cards for a hiker's swipes to settle before the deck runs out.
let private hills = {
    TripBuilderTests.catalog with
        Cards =
            TripBuilderTests.catalog.Cards
            @ [
                for index in 1..12 -> TripBuilderTests.card $"hill-{index}" "interest:mountains" (Some "peak")
            ]
}

[<Fact>]
let ``a clear taste settles early and still gets a trip`` () =
    let session = Session.play hills (ContractMapping.journeyGraph hills) persona

    match session.Early with
    | Some finish ->
        Assert.InRange(finish.Cards, JourneyPath.settledAfter + 1, session.Answers.Length - 1)
        Assert.True(Result.isOk finish.Trip)
    | None -> failwith "expected the swipes to settle"
