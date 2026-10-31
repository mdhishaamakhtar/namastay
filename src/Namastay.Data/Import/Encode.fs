module Namastay.Data.Import.Encode

open Namastay.Domain.Catalog

let status =
    function
    | FactStatus.Official -> "official"
    | FactStatus.TwoSources -> "two_sources"
    | FactStatus.OneSource -> "one_source"
    | FactStatus.Conflict -> "conflict"
    | FactStatus.Editorial -> "editorial"
    | FactStatus.Unverified -> "unverified"

let gate =
    function
    | Gate.Open -> "open"
    | Gate.Warn -> "warn"
    | Gate.Permit -> "permit"

let popularity =
    function
    | Popularity.Mainstream -> "mainstream"
    | Popularity.Between -> "between"
    | Popularity.Offbeat -> "offbeat"

let rating =
    function
    | Rating.Best -> "best"
    | Rating.Ok -> "ok"
    | Rating.Avoid -> "avoid"
    | Rating.Closed -> "closed"
    | Rating.Unknown -> "unknown"

let traveller =
    function
    | Traveller.Solo -> "solo"
    | Traveller.Couple -> "couple"
    | Traveller.Friends -> "friends"
    | Traveller.Family -> "family"

let costLevel =
    function
    | CostLevel.Budget -> "budget"
    | CostLevel.Mid -> "mid"
    | CostLevel.Luxury -> "luxury"

let mode =
    function
    | Mode.Flight -> "flight"
    | Mode.Train -> "train"
    | Mode.Road -> "road"
    | Mode.Ferry -> "ferry"
    | Mode.CableCar -> "cable_car"

let scope =
    function
    | NoticeScope.National -> "national"
    | NoticeScope.State -> "state"
    | NoticeScope.PlaceScope -> "place"
    | NoticeScope.LinkScope -> "link"
    | NoticeScope.FestivalScope -> "festival"
