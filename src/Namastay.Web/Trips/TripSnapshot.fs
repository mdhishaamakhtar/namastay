/// Shared links must keep working forever, so stored trips are upgraded as they're read.
module Namastay.Web.Trips.TripSnapshot

open System.Text.Json
open System.Text.Json.Nodes
open Namastay.Shared.Contracts
open Namastay.Web.Http

let private ensure (name: string) (fallback: unit -> JsonNode) (node: JsonObject) =
    if not (node.ContainsKey name) then
        node[name] <- fallback ()

/// Fills fields added after the trip was saved.
let private upgrade (trip: JsonObject) =
    ensure "mix" (fun () -> JsonArray()) trip
    ensure "month" (fun () -> null) trip
    ensure "arrive" (fun () -> null) trip
    ensure "depart" (fun () -> null) trip
    ensure "diet" (fun () -> null) trip

    match trip["stops"] with
    | :? JsonArray as stops ->
        for stop in stops do
            ensure "photo" (fun () -> null) (stop.AsObject())
            ensure "food" (fun () -> null) (stop.AsObject())
            ensure "custom" (fun () -> JsonValue.Create false) (stop.AsObject())
    | _ -> ()

let read (text: string) : Trip option =
    try
        let trip = (JsonNode.Parse text).AsObject()
        upgrade trip
        Some(trip.Deserialize<Trip> Json.options)
    with
    | :? JsonException
    | :? System.InvalidOperationException -> None
