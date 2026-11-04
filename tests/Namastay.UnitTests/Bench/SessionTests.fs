module Namastay.UnitTests.Bench.SessionTests

open Xunit
open Namastay.Bench
open Namastay.Bench.Personas
open Namastay.Domain.Catalog
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
