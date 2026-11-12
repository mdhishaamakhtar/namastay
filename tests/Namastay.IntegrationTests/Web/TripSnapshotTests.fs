module Namastay.IntegrationTests.Web.TripSnapshotTests

open System.Text.Json.Nodes
open Xunit
open Namastay.Web.Trips

let private colours =
    JsonObject.Parse """{"deep":"#000000","mid":"#111111","pale":"#222222","textOnDeep":"#ffffff"}"""

/// A trip saved before food existed.
let private stored =
    $$"""
    {
      "title": "Old trip", "baseFamily": "pine", "stops": [
        {
          "placeSlug": "manali", "name": "Manali", "days": 5, "familySlug": "pine",
          "colours": {{colours.ToJsonString()}}, "access": "Open", "stayArea": null,
          "plans": [], "arrival": null, "retired": false
        }
      ],
      "swaps": [], "leftOut": [], "notices": [], "bestMonths": [], "budget": "Mid", "dataVersion": 1
    }
    """

[<Fact>]
let ``a trip saved before food existed still opens without any`` () =
    let trip = TripSnapshot.read stored
    Assert.True(trip.IsSome, "the old trip should still read")
    Assert.True(trip.Value.Diet.IsNone)
    Assert.True(trip.Value.Stops.Head.Food.IsNone)

[<Fact>]
let ``a trip saved before stops had reasons still opens without any`` () =
    let trip = TripSnapshot.read stored
    Assert.True(trip.Value.Stops.Head.Reason.IsNone)
