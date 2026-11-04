module Namastay.UnitTests.Trip.ScopeTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Shared.Contracts

let private catalog = {
    TripBuilderTests.catalog with
        Places = [
            TripBuilderTests.place "peak" "c-hills" "pine" "mountains"
            {
                TripBuilderTests.place "foothill" "c-hills" "pine" "mountains" with
                    Interests = [
                        {
                            InterestSlug = "mountains"
                            Weight = 2
                        }
                    ]
            }
            {
                TripBuilderTests.place "hidden" "c-hills" "pine" "mountains" with
                    Published = false
            }
        ]
}

let private graph = ContractMapping.journeyGraph TripBuilderTests.catalog

[<Fact>]
let ``a feeling's places suit it strongly and are published`` () =
    Assert.Equal<Set<string>>(Set.ofList [ "peak" ], Scope.feelingPlaces catalog "mountains")

[<Fact>]
let ``a thin feeling deck keeps every card from its places`` () =
    let scope = {
        Kind = ScopeKind.Feeling
        Slug = "mountains"
    }

    let kept = Scope.within scope (Set.ofList [ "peak" ]) graph
    Assert.Equal<string list>([ "peak-card" ], kept.Cards |> List.map _.Slug)

[<Fact>]
let ``a food trip keeps only the food cards`` () =
    let scope = {
        Kind = ScopeKind.FoodInRegion
        Slug = "hills"
    }

    let kept = Scope.within scope (Set.ofList [ "peak"; "quiet-beach" ]) graph
    Assert.Empty kept.Cards

let private peakCard slug interest = {
    TripBuilderTests.card slug interest (Some "peak") with
        Signals = [ { Signal = interest; Weight = 3 } ]
}

let private fullDeck = {
    TripBuilderTests.catalog with
        Interests =
            {
                Slug = "snow-peaks"
                Name = "Snow peaks"
                ParentSlug = Some "mountains"
                Colour = None
            }
            :: TripBuilderTests.catalog.Interests
        Cards = [
            for index in 1..11 do
                peakCard $"climb-{index}" "interest:mountains"
            peakCard "snow" "interest:snow-peaks"
            peakCard "eat" "interest:food"
            peakCard "dance" "interest:nightlife"
        ]
}

[<Fact>]
let ``a feeling deck keeps the cards that speak for it, children included`` () =
    let scope = {
        Kind = ScopeKind.Feeling
        Slug = "mountains"
    }

    let kept =
        Scope.within scope (Set.ofList [ "peak" ]) (ContractMapping.journeyGraph fullDeck)

    let slugs = kept.Cards |> List.map _.Slug
    Assert.Equal(12, slugs.Length)
    Assert.Contains("snow", slugs)
    Assert.DoesNotContain("eat", slugs)
    Assert.DoesNotContain("dance", slugs)
