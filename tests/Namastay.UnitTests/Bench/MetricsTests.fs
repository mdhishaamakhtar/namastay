module Namastay.UnitTests.Bench.MetricsTests

open Xunit
open Namastay.Bench
open Namastay.Bench.Metrics
open Namastay.Bench.Personas
open Namastay.Bench.Session
open Namastay.Domain.Catalog
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip

let private catalog = TripBuilderTests.catalog
let private graph = ContractMapping.journeyGraph catalog

let private hiker = {
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

let private session = Session.play catalog graph hiker

let private trip =
    match session.Trip with
    | Ok trip -> trip
    | Error error -> failwith $"expected a trip, got {error}"

let private gateway city = {
    City = city
    Airport = "XXX"
    Via = None
    Leg = None
}

let private value name sessions =
    (Metrics.scorecard catalog sessions |> List.find (fun line -> line.Name = name)).Value

let private withTrip (trip: Trip) = { session with Trip = Ok trip }

[<Fact>]
let ``landing where the trip ends is not a city twice`` () =
    let last = (List.last trip.Stops).Name

    let ends = {
        trip with
            Arrive = Some(gateway last)
            Depart = Some(gateway last)
    }

    Assert.Equal(0.0, value "city twice %" [ withTrip ends ])

[<Fact>]
let ``landing at a middle stop is a city twice`` () =
    Assert.True(trip.Stops.Length >= 3, "the fixture trip needs a middle stop")
    let middle = trip.Stops[1].Name

    let twice = {
        trip with
            Arrive = Some(gateway middle)
    }

    Assert.Equal(100.0, value "city twice %" [ withTrip twice ])

[<Fact>]
let ``a one-stop trip is never a city twice`` () =
    let only = {
        trip with
            Stops = [ trip.Stops.Head ]
            Arrive = Some(gateway trip.Stops.Head.Name)
            Depart = Some(gateway trip.Stops.Head.Name)
    }

    Assert.Equal(0.0, value "city twice %" [ withTrip only ])

[<Fact>]
let ``a repeated stop is a city twice`` () =
    let repeated = {
        trip with
            Stops = trip.Stops @ [ trip.Stops.Head ]
    }

    Assert.Equal(100.0, value "city twice %" [ withTrip repeated ])

[<Fact>]
let ``a trip on taste spends every day on it`` () =
    Assert.Equal(100.0, value "days on taste %" [ session ])
    Assert.Equal(100.0, value "top interest in trip %" [ session ])

[<Fact>]
let ``first cards count the open deck only`` () =
    let scoped = Session.play catalog graph { hiker with Scoped = true; Seed = 9 }
    Assert.Equal(1.0, value "first cards, open deck" [ session; scoped; session ])

[<Fact>]
let ``a trip that failed to build is counted`` () =
    let failed = {
        session with
            Trip = Error Namastay.Domain.Trip.Builder.TripError.NoPlaces
    }

    Assert.Equal(1.0, value "trips not built" [ session; failed ])

[<Fact>]
let ``every line has a direction`` () =
    let lines = Metrics.scorecard catalog [ session ]
    Assert.Equal(17, lines.Length)
    Assert.Equal(lines.Length, (lines |> List.map _.Name |> List.distinct).Length)
