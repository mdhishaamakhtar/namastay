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

let private stop (slug: string) = {
    trip.Stops.Head with
        PlaceSlug = slug
        Name = slug.ToUpperInvariant()
}

[<Fact>]
let ``landing at a middle stop is a city twice`` () =
    let twice = {
        trip with
            Stops = [ stop "peak"; stop "valley"; stop "lake" ]
            Arrive = Some(gateway "VALLEY")
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

let private early cards (found: Session) = {
    found with
        Early = Some { Cards = cards; Trip = found.Trip }
}

[<Fact>]
let ``pace counts the tasteful travellers who settled early`` () =
    let sessions = [ early 10 session; early 14 session; session; session ]
    Assert.Equal(50.0, value "taste settles early %" sessions)
    Assert.Equal(12.0, value "cards to settle" sessions)
    Assert.Equal(100.0, value "top interest in early trip %" sessions)

[<Fact>]
let ``an early trip that missed the taste or failed is not a hit`` () =
    let failed = {
        session with
            Early =
                Some {
                    Cards = 9
                    Trip = Error Namastay.Domain.Trip.Builder.TripError.NoPlaces
                }
    }

    let elsewhere = {
        session with
            Early =
                Some {
                    Cards = 9
                    Trip = Ok { trip with Stops = [ stop "market" ] }
                }
    }

    Assert.Equal(50.0, value "top interest in early trip %" [ early 9 session; failed ])
    Assert.Equal(0.0, value "top interest in early trip %" [ elsewhere ])

[<Fact>]
let ``coin flippers who settle early are counted on their own`` () =
    let flipper = {
        session with
            Persona = { hiker with Swiper = Swiper.Random }
    }

    Assert.Equal(50.0, value "coin flip settles early %" [ early 9 flipper; flipper; early 9 session ])
    Assert.Equal(100.0, value "taste settles early %" [ early 9 flipper; flipper; early 9 session ])

[<Fact>]
let ``every line has a direction`` () =
    let lines = Metrics.scorecard catalog [ session ]
    Assert.Equal(21, lines.Length)
    Assert.Equal(lines.Length, (lines |> List.map _.Name |> List.distinct).Length)
