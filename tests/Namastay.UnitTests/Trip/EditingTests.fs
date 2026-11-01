module Namastay.UnitTests.Trip.EditingTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip.TripBuilderTests

let known slug days = {
    PlaceSlug = Some slug
    CustomName = None
    Days = days
}

let custom name days = {
    PlaceSlug = None
    CustomName = Some name
    Days = days
}

let original = build catalog (request (liked [ "likes-hills" ]) 7 None)

let edit stops = {
    Title = "My trip"
    Month = None
    Stops = stops
}

let blocked result =
    match result with
    | Error reasons -> reasons
    | Ok _ -> raise (Xunit.Sdk.XunitException "expected the edit to be blocked")

let applied result =
    match result with
    | Ok(edited: EditedTrip) -> edited
    | Error reasons -> raise (Xunit.Sdk.XunitException $"expected success, got {reasons}")

let apply tripEdit = Editing.apply catalog original tripEdit

[<Fact>]
let ``reordering connected stops works`` () =
    let edited = apply (edit [ known "valley" 3; known "peak" 4 ]) |> applied
    Assert.Equal<string list>([ "valley"; "peak" ], edited.Trip.Stops |> List.map _.PlaceSlug)
    Assert.Equal("My trip", edited.Trip.Title)

[<Fact>]
let ``travel legs follow the new order`` () =
    let edited = apply (edit [ known "valley" 3; known "peak" 4 ]) |> applied
    Assert.True(edited.Trip.Stops.Head.Arrival.IsNone)
    Assert.True(edited.Trip.Stops[1].Arrival.IsSome)

[<Fact>]
let ``broken route is blocked`` () =
    let reasons = apply (edit [ known "peak" 3; known "party-beach" 4 ]) |> blocked
    Assert.Contains(reasons, (fun reason -> reason.Contains "no route"))

[<Fact>]
let ``goa in july warns but saves`` () =
    let tripEdit = {
        edit [ known "party-beach" 5 ] with
            Month = Some 7
    }

    let edited = apply tripEdit |> applied
    Assert.Contains("July", edited.Warnings.Head)
    Assert.Equal(Some 7, edited.Trip.Month)

[<Fact>]
let ``custom place is marked unchecked`` () =
    let edited =
        apply (edit [ known "peak" 3; custom "My cousin's house" 4 ]) |> applied

    let stop = edited.Trip.Stops[1]
    Assert.True stop.Custom
    Assert.Equal("My cousin's house", stop.Name)
    Assert.Contains(edited.Warnings, (fun warning -> warning.Contains "haven't checked"))

[<Fact>]
let ``custom stops do not break the route`` () =
    let edited =
        apply (edit [ custom "A beach" 3; known "peak" 2; custom "A town" 2 ])
        |> applied

    Assert.Equal(3, edited.Trip.Stops.Length)

[<Fact>]
let ``blank custom name is blocked`` () =
    let reasons = apply (edit [ known "peak" 3; custom "  " 4 ]) |> blocked
    Assert.Contains(reasons, (fun reason -> reason.Contains "name"))

[<Fact>]
let ``permit place keeps notice`` () =
    let gated = {
        place "permit-peak" "c-hills" "pine" "mountains" with
            Gate = Gate.Permit
    }

    let world = {
        catalog with
            Places = gated :: catalog.Places
    }

    let edited =
        Editing.apply world original (edit [ known "permit-peak" 5 ]) |> applied

    Assert.Contains(edited.Trip.Notices, (fun notice -> notice.Severity = NoticeSeverity.Permit))

[<Fact>]
let ``excluded place is blocked`` () =
    let world = {
        catalog with
            Exclusions = [
                {
                    Slug = "peak"
                    Name = "Peak"
                    Reason = "Advisory."
                }
            ]
    }

    let reasons = Editing.apply world original (edit [ known "peak" 5 ]) |> blocked
    Assert.Contains(reasons, (fun reason -> reason.Contains "can't"))

[<Fact>]
let ``unknown place is blocked`` () =
    let reasons = apply (edit [ known "atlantis" 5 ]) |> blocked
    Assert.Contains(reasons, (fun reason -> reason.Contains "atlantis"))

[<Fact>]
let ``empty trip is blocked`` () =
    Assert.NotEmpty(apply (edit []) |> blocked)

[<Theory>]
[<InlineData(4)>]
[<InlineData(22)>]
let ``days outside 5 to 21 are blocked`` days =
    Assert.NotEmpty(apply (edit [ known "peak" days ]) |> blocked)

[<Fact>]
let ``a stop needs at least one day`` () =
    Assert.NotEmpty(apply (edit [ known "peak" 7; known "valley" 0 ]) |> blocked)

[<Fact>]
let ``base family follows the edited days`` () =
    let edited = apply (edit [ known "fort" 3; custom "Somewhere" 4 ]) |> applied
    Assert.Equal("stone", edited.Trip.BaseFamily)

[<Fact>]
let ``generated trips have no blocks`` () =
    for days in [ 5; 7; 10; 14; 21 ] do
        let trip =
            build catalog (request (liked [ "likes-hills"; "likes-beach" ]) days None)

        let stops =
            trip.Stops
            |> List.map (fun stop -> {
                PlaceSlug = Some stop.PlaceSlug
                CustomName = None
                Days = stop.Days
            })

        let result =
            Editing.apply catalog trip {
                Title = trip.Title
                Month = None
                Stops = stops
            }

        Assert.True(result.IsOk)
