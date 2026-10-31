module Namastay.Data.Catalog.Decode

open Namastay.Data.Import
open Namastay.Domain.Catalog

/// Reverses an `Encode` function, so the two can't drift apart.
let private decoder (encode: 'Value -> string) (cases: 'Value list) =
    let known = cases |> List.map (fun case -> encode case, case) |> Map.ofList
    fun (text: string) -> known[text]

let status =
    decoder Encode.status [
        FactStatus.Official
        FactStatus.TwoSources
        FactStatus.OneSource
        FactStatus.Conflict
        FactStatus.Editorial
        FactStatus.Unverified
    ]

let gate = decoder Encode.gate [ Gate.Open; Gate.Warn; Gate.Permit ]

let popularity =
    decoder Encode.popularity [ Popularity.Mainstream; Popularity.Between; Popularity.Offbeat ]

let rating =
    decoder Encode.rating [ Rating.Best; Rating.Ok; Rating.Avoid; Rating.Closed; Rating.Unknown ]

let traveller =
    decoder Encode.traveller [ Traveller.Solo; Traveller.Couple; Traveller.Friends; Traveller.Family ]

let costLevel =
    decoder Encode.costLevel [ CostLevel.Budget; CostLevel.Mid; CostLevel.Luxury ]

let mode =
    decoder Encode.mode [ Mode.Flight; Mode.Train; Mode.Road; Mode.Ferry; Mode.CableCar ]

let scope =
    decoder Encode.scope [
        NoticeScope.National
        NoticeScope.State
        NoticeScope.PlaceScope
        NoticeScope.LinkScope
        NoticeScope.FestivalScope
    ]
