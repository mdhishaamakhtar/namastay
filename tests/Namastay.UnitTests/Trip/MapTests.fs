/// What a trip knows about where its stops are, for drawing its route.
module Namastay.UnitTests.Trip.MapTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip.TripBuilderTests

let private at latitude longitude (found: Place) = {
    found with
        Position =
            Some {
                Latitude = latitude
                Longitude = longitude
            }
}

let private point latitude longitude : MapPoint = {
    Latitude = latitude
    Longitude = longitude
}

let private world = {
    catalog with
        Places = [
            place "remote" "c-hills" "pine" "mountains" |> at 32.0 77.0
            hubAirport "main" "c-plains" "stone" "forts" |> at 28.6 77.2
            hubAirport "far-hub" "c-plains" "stone" "forts" |> at 19.0 72.8
            hubAirport "unplaced-hub" "c-plains" "stone" "forts"
            {
                at 13.0 80.2 (hubAirport "hidden-hub" "c-plains" "stone" "forts") with
                    Published = false
            }
        ]
        Links = both "main" "remote"
        Cards = [ card "likes-remote" "interest:mountains" (Some "remote") ]
}

let private trip = build world (request (liked [ "likes-remote" ]) 6 None)

[<Fact>]
let ``a stop knows where it is on the map`` () =
    let remote = trip.Stops |> List.find (fun stop -> stop.PlaceSlug = "remote")
    Assert.Equal(Some(point 32.0 77.0), remote.Position)

[<Fact>]
let ``the main cities a trip skips are landmarks to find it by`` () =
    Assert.Equal<MapLandmark list>(
        [
            {
                Name = "FAR-HUB"
                Position = point 19.0 72.8
            }
        ],
        trip.Landmarks
    )

[<Fact>]
let ``a place with no position leaves its stop off the map`` () =
    let unplaced = {
        world with
            Places = world.Places |> List.map (fun found -> { found with Position = None })
    }

    let built = build unplaced (request (liked [ "likes-remote" ]) 6 None)
    Assert.All(built.Stops, (fun stop -> Assert.Equal(None, stop.Position)))
    Assert.Empty built.Landmarks

[<Fact>]
let ``a city edited out of a trip becomes a landmark`` () =
    let change: TripEdit = {
        Title = "Just the hills"
        Month = None
        Stops = [
            {
                PlaceSlug = Some "remote"
                CustomName = None
                Days = 6
            }
        ]
    }

    match Namastay.Domain.Trip.Editing.apply world trip change with
    | Ok edited -> Assert.Equal<string list>([ "MAIN"; "FAR-HUB" ], edited.Trip.Landmarks |> List.map _.Name)
    | Error reasons -> Assert.Fail $"expected the edit to save, got {reasons}"
