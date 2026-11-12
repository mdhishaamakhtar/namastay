module Namastay.UnitTests.Bench.SwipersTests

open System
open Xunit
open Namastay.Bench
open Namastay.Bench.Personas
open Namastay.Domain.Catalog
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip

let private interests = [
    {
        Slug = "beaches"
        Name = "Beaches"
        ParentSlug = None
        Colour = None
    }
    {
        Slug = "quiet-beaches"
        Name = "Quiet beaches"
        ParentSlug = Some "beaches"
        Colour = None
    }
    {
        Slug = "mountains"
        Name = "Mountains"
        ParentSlug = None
        Colour = None
    }
]

let private persona swiper interests = {
    Name = "test"
    Swiper = swiper
    Interests = interests
    Days = 7
    Month = None
    Travellers = TravellerType.Solo
    Budget = BudgetLevel.Mid
    Comfort = TransportComfort.Anything
    Feelings = []
    Seed = 1
}

[<Fact>]
let ``a child interest suits its parent`` () =
    Assert.True(Swipers.suits interests [ "beaches" ] "quiet-beaches")

[<Fact>]
let ``a parent does not suit its child`` () =
    Assert.False(Swipers.suits interests [ "quiet-beaches" ] "beaches")

[<Fact>]
let ``an unrelated interest does not suit`` () =
    Assert.False(Swipers.suits interests [ "beaches" ] "mountains")

[<Fact>]
let ``everything likes every card`` () =
    let card = TripBuilderTests.card "any" "interest:mountains" None

    Assert.True(Swipers.decide (Random 1) TripBuilderTests.catalog (persona Swiper.Everything []) card)

[<Fact>]
let ``taste mostly likes what matches and mostly passes what does not`` () =
    let random = Random 3
    let liked = TripBuilderTests.card "liked" "interest:mountains" None
    let other = TripBuilderTests.card "other" "interest:beaches" None
    let wants = persona Swiper.Taste [ "mountains" ]

    let likes card =
        [ 1..200 ]
        |> List.filter (fun _ -> Swipers.decide random TripBuilderTests.catalog wants card)
        |> List.length

    Assert.InRange(likes liked, 140, 200)
    Assert.InRange(likes other, 0, 60)
