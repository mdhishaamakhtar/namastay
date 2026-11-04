module Namastay.UnitTests.Catalog.JourneyGraphTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.UnitTests.Trip

[<Fact>]
let ``journey graph names the strongest interest and keeps only known branches`` () =
    let card = {
        Slug = "two-signals"
        PlaceSlug = Some "peak"
        Title = "Two"
        Line = "Two signals."
        PhotoSlug = None
        IllustrationSlug = None
        IsStart = true
        Landing = None
        Branches = [
            {
                OnAnswer = true
                ToCard = "peak-card"
                Priority = 1
            }
            {
                OnAnswer = false
                ToCard = "gone"
                Priority = 2
            }
        ]
        Signals = [
            {
                Signal = "interest:beaches"
                Weight = 2
            }
            {
                Signal = "interest:mountains"
                Weight = 3
            }
        ]
    }

    let catalog = {
        TripBuilderTests.catalog with
            Cards = card :: TripBuilderTests.catalog.Cards
    }

    let graph = ContractMapping.journeyGraph catalog
    let mapped = graph.Cards |> List.find (fun item -> item.Slug = "two-signals")
    Assert.Equal(catalog.Cards.Length, graph.Cards.Length)
    Assert.Equal(Some "Name of mountains", mapped.Interest)
    Assert.Equal<string list>([ "mountains"; "beaches" ], mapped.Interests |> List.map _.Slug)
    Assert.Equal(catalog.Interests.Length, graph.Interests.Length)
    Assert.Equal(Some "peak", mapped.Place)
    Assert.Equal(Some "hills", mapped.Region)
    Assert.Equal(Some "#111111", mapped.Colours |> Option.map _.Deep)
    Assert.Equal<string list>([ "peak-card" ], mapped.Branches |> List.map _.ToCard)
    Assert.True mapped.IsStart
    Assert.Equal(None, mapped.ImageUrl)

[<Fact>]
let ``a card without a place has no colours or region`` () =
    let graph = ContractMapping.journeyGraph TripBuilderTests.catalog
    let plain = graph.Cards |> List.find (fun item -> item.Slug = "likes-hills")
    Assert.Equal(None, plain.Colours)
    Assert.Equal(None, plain.Region)
    Assert.Equal(None, plain.Place)
