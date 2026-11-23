/// What a stop says there is to do, taken from the place's guide.
module Namastay.UnitTests.Trip.HighlightsTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip.TripBuilderTests

let private experience name sort status = {
    Slug = name
    Name = name
    Description = $"About {name}."
    Sort = sort
    Evidence = { evidence with Status = status }
}

let private world (experiences: Experience list) = {
    catalog with
        Places = [
            {
                place "peak" "c-hills" "pine" "mountains" with
                    Experiences = experiences
            }
        ]
}

let private highlights experiences =
    (build (world experiences) (request (liked [ "likes-hills" ]) 5 None)).Stops.Head.Highlights

[<Fact>]
let ``a stop says what there is to do, in the guide's order`` () =
    let found =
        highlights [
            experience "second" 2 FactStatus.Editorial
            experience "first" 1 FactStatus.Official
        ]

    Assert.Equal<TripHighlight list>(
        [
            {
                Name = "first"
                Description = "About first."
            }
            {
                Name = "second"
                Description = "About second."
            }
        ],
        found
    )

[<Fact>]
let ``an unverified thing to do is left out of a trip`` () =
    let found =
        highlights [
            experience "rumour" 1 FactStatus.Unverified
            experience "sure" 2 FactStatus.Editorial
        ]

    Assert.Equal<string list>([ "sure" ], found |> List.map _.Name)

[<Fact>]
let ``a stop shows no more than four things to do`` () =
    let found =
        highlights [ for sort in 1..6 -> experience $"thing-{sort}" sort FactStatus.Editorial ]

    Assert.Equal<string list>([ "thing-1"; "thing-2"; "thing-3"; "thing-4" ], found |> List.map _.Name)
