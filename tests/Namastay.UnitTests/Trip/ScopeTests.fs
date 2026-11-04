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
let ``a feeling keeps every card from its places`` () =
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
