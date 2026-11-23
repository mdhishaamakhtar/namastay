/// How much of a trip goes on getting around, and how honestly a leg is described.
module Namastay.UnitTests.Trip.TravelTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip.TripBuilderTests

let private flown from target minutes =
    timed from target minutes
    |> List.map (fun link -> { link with Mode = Mode.Flight })

let private mountains slug = place slug "c-hills" "pine" "mountains"
/// Needs three days, so a short trip has room for only one of them beside its base.
let private slow slug = { mountains slug with DaysMin = 3 }

let private mainHub = hubAirport "main" "c-plains" "stone" "forts"

let private likes slug =
    card ("likes-" + slug) "interest:mountains" (Some slug)

let private world places links liked = {
    catalog with
        Places = places
        Links = links
        Cards = liked |> List.map likes
}

let private tripFor places links (loved: string list) days =
    build (world places links loved) (request (liked (loved |> List.map (fun slug -> "likes-" + slug))) days None)

let private stitched (trip: Trip) =
    trip.Stops
    |> List.choose _.Arrival
    |> List.find (fun leg -> leg.ComfortNote.IsSome)

[<Fact>]
let ``a leg stitched from a flight and a drive is a flight that says both parts`` () =
    let trip =
        tripFor
            [ mountains "north"; mainHub; mountains "south" ]
            (flown "north" "main" 100 @ timed "main" "south" 200)
            [ "north"; "south" ]
            6

    let leg = stitched trip
    Assert.Equal(TravelMode.Flight, leg.Mode)
    let note = leg.ComfortNote.Value
    Assert.Contains("MAIN", note)
    Assert.True(note.StartsWith "Fly to" || note.EndsWith "by air.", note)
    Assert.True(note.StartsWith "Drive to" || note.EndsWith "by road.", note)

[<Fact>]
let ``a leg stitched from two ground journeys takes the mode of the longer one`` () =
    let rail =
        timed "main" "south" 200
        |> List.map (fun link -> { link with Mode = Mode.Train })

    let trip =
        tripFor
            [ mountains "north"; mainHub; mountains "south" ]
            (timed "north" "main" 100 @ rail)
            [ "north"; "south" ]
            6

    Assert.Equal(TravelMode.Train, (stitched trip).Mode)

[<Fact>]
let ``a place reached directly beats an equal one reached by changing in a city`` () =
    let trip =
        tripFor
            [ mountains "base"; mainHub; slow "direct"; slow "beyond" ]
            (timed "base" "direct" 250
             @ timed "direct" "main" 150
             @ timed "base" "main" 100
             @ timed "main" "beyond" 100)
            [ "base"; "direct"; "beyond" ]
            6

    Assert.Contains("direct", slugs trip)
    Assert.DoesNotContain("beyond", slugs trip)

[<Fact>]
let ``a route counts how far its stops are from the airport, not just that there is one`` () =
    let trip =
        tripFor
            [ mainHub; slow "near"; slow "far" ]
            (timed "main" "near" 60 @ timed "main" "far" 540)
            [ "near"; "far" ]
            5

    Assert.Equal<string list>([ "near"; "main" ], slugs trip)

[<Fact>]
let ``a week gives up one day to travel sooner than two`` () =
    let trip =
        tripFor
            [ mountains "base"; mainHub; slow "one-haul"; slow "two-hauls" ]
            (timed "main" "base" 60
             @ timed "base" "one-haul" 620
             @ timed "one-haul" "main" 60
             @ timed "base" "two-hauls" 300
             @ timed "two-hauls" "main" 300)
            [ "base"; "one-haul"; "two-hauls" ]
            7

    Assert.Contains("one-haul", slugs trip)
    Assert.DoesNotContain("two-hauls", slugs trip)

[<Fact>]
let ``the last city takes the days the other stops can't honestly fill`` () =
    let trip =
        tripFor [ mainHub; mountains "remote" ] (both "main" "remote") [ "remote" ] 8

    Assert.Equal<(string * int) list>(
        [ "remote", 4; "main", 4 ],
        trip.Stops |> List.map (fun stop -> stop.PlaceSlug, stop.Days)
    )

[<Fact>]
let ``the last city stays short while the other stops have room`` () =
    let roomy = { mountains "remote" with DaysMax = 9 }
    let trip = tripFor [ mainHub; roomy ] (both "main" "remote") [ "remote" ] 8

    Assert.Equal<(string * int) list>(
        [ "remote", 6; "main", 2 ],
        trip.Stops |> List.map (fun stop -> stop.PlaceSlug, stop.Days)
    )

[<Fact>]
let ``a hub in a hard month still ends the trip when the fine one is across the country`` () =
    let trip =
        likingRemote
            [
                {
                    hubAirport "near-hub" "c-plains" "stone" "forts" with
                        Months = months [] [ 5 ]
                }
                hubAirport "far-hub" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (timed "near-hub" "remote" 70 @ timed "far-hub" "remote" 600)
            6
            (Some 5)

    Assert.Equal("near-hub", (List.last trip.Stops).PlaceSlug)

[<Fact>]
let ``spare days go to the last city before a place nobody asked for`` () =
    // The hub is in a hard month, so it can end the trip but isn't somewhere to route through.
    let hardHub = {
        mainHub with
            Months = months [] [ 5 ]
    }

    let trip =
        likingRemote
            [ hardHub; mountains "remote"; mountains "filler" ]
            (both "main" "remote" @ both "remote" "filler")
            8
            (Some 5)

    Assert.Equal<string list>([ "remote"; "main" ], slugs trip)

[<Fact>]
let ``a trip doesn't pass through one main city to fly home from another`` () =
    let trip =
        likingRemote
            [
                {
                    hubAirport "near-hub" "c-plains" "stone" "forts" with
                        Months = months [] [ 5 ]
                }
                hubAirport "far-hub" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (timed "near-hub" "remote" 400 @ timed "near-hub" "far-hub" 100)
            6
            (Some 5)

    Assert.Equal("near-hub", (List.last trip.Stops).PlaceSlug)

[<Fact>]
let ``a trip beside an international airport flies home from it, not from a hub further on`` () =
    let trip =
        tripFor
            [ mainHub; airport "port" "c-plains" "stone" "forts"; mountains "remote" ]
            (timed "remote" "port" 100 @ flown "port" "main" 70)
            [ "remote" ]
            6

    Assert.Equal<string list>([ "remote"; "port" ], slugs trip)
    Assert.Equal("PORT", trip.Depart.Value.City)

[<Fact>]
let ``a trip that ends in an airport city far from any hub flies home from there`` () =
    let trip =
        tripFor [ mainHub; airport "remote" "c-hills" "pine" "mountains" ] (timed "remote" "main" 400) [ "remote" ] 5

    Assert.Equal<string list>([ "remote" ], slugs trip)
    Assert.Equal("REMOTE", trip.Depart.Value.City)

[<Fact>]
let ``an airport city between two stops moves to the end, where the trip flies home`` () =
    let trip =
        tripFor
            [
                mainHub
                airport "port" "c-plains" "stone" "forts"
                mountains "north"
                mountains "south"
            ]
            (timed "north" "port" 100 @ timed "port" "south" 100 @ flown "port" "main" 70)
            [ "north"; "south" ]
            9

    Assert.Equal("port", (List.last trip.Stops).PlaceSlug)
    Assert.Equal(1, trip.Stops |> List.filter (fun stop -> stop.PlaceSlug = "port") |> List.length)
    Assert.Equal("PORT", trip.Depart.Value.City)

[<Fact>]
let ``a full trip still moves its airport city to the end instead of dropping a stop`` () =
    let places = [
        mainHub
        airport "port" "c-plains" "stone" "forts"
        mountains "north"
        mountains "south"
    ]

    let links =
        timed "north" "port" 100 @ timed "port" "south" 100 @ flown "port" "main" 70

    // Friends keep every stop short, so six days leave none to spare.
    let trip =
        build (world places links [ "north"; "south"; "port" ]) {
            request (liked [ "likes-north"; "likes-south"; "likes-port" ]) 6 None with
                Travellers = TravellerType.Friends
        }

    Assert.Equal<string list>([ "north"; "port"; "south" ], slugs trip |> List.sort)
    Assert.Equal("port", (List.last trip.Stops).PlaceSlug)

[<Fact>]
let ``an airport city with no guide doesn't stop a trip ending in a main city further on`` () =
    let hidden = {
        airport "port" "c-plains" "stone" "forts" with
            Published = false
    }

    let trip =
        tripFor
            [ mainHub; hidden; mountains "remote" ]
            (timed "remote" "port" 100 @ timed "remote" "main" 300)
            [ "remote" ]
            6

    Assert.Equal("main", (List.last trip.Stops).PlaceSlug)
