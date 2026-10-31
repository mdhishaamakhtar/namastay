module Namastay.UnitTests.Trip.TripBuilderTests

open System
open Xunit
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Shared.Contracts

let evidence = {
    Status = FactStatus.Editorial
    LastChecked = DateOnly(2026, 10, 1)
    SourceIds = []
}

let months best avoid = [
    for month in 1..12 ->
        {
            Month = month
            Rating =
                if List.contains month best then Rating.Best
                elif List.contains month avoid then Rating.Avoid
                else Rating.Ok
            Note = None
            Evidence = evidence
        }
]

let place slug cluster family interest : Place = {
    Slug = slug
    ClusterSlug = cluster
    Name = slug.ToUpperInvariant()
    State = "State"
    FamilySlug = family
    Shade = "deep"
    Gate = Gate.Open
    Popularity = Popularity.Mainstream
    DaysMin = 2
    DaysMax = 4
    CostLow = Some 1
    CostHigh = Some 3
    Summary = "Summary."
    IsThisForYou = None
    Published = true
    Interests = [ { InterestSlug = interest; Weight = 5 } ]
    Months = months [ 10; 11 ] []
    TravellerFit = []
    Experiences = []
    DayPlans = [
        {
            Variant = "short"
            Day = 1
            Plan = "Look around."
            Evidence = evidence
        }
    ]
    StayAreas = [
        {
            Name = "Centre"
            CostLevel = CostLevel.Budget
            Note = None
            Evidence = evidence
        }
    ]
    Evidence = evidence
}

let road from target = {
    Slug = from + "-" + target
    FromSlug = from
    ToSlug = target
    Mode = Mode.Road
    MinutesMin = 120
    MinutesMax = 180
    DistanceKm = None
    RunDays = None
    ValidFrom = None
    ValidTo = None
    ComfortNote = None
    Trains = []
    Evidence = evidence
}

let both from target = [ road from target; road target from ]

let flight from target = {
    road from target with
        Slug = "fly-" + from + "-" + target
        Mode = Mode.Flight
}

let card slug signal placeSlug = {
    Slug = slug
    PlaceSlug = placeSlug
    Title = slug
    Line = slug
    PhotoSlug = None
    IllustrationSlug = None
    IsStart = false
    Branches = []
    Signals = [ { Signal = signal; Weight = 3 } ]
}

let family slug = {
    Slug = slug
    Name = slug
    UsedFor = slug
    Deep = "#111111"
    Mid = "#222222"
    Pale = "#333333"
    TextOnDeep = "#FFFFFF"
}

let catalog = {
    Catalog.empty with
        Regions = [
            { Slug = "hills"; Name = "Hills" }
            { Slug = "coast"; Name = "Coast" }
            { Slug = "plains"; Name = "Plains" }
        ]
        Clusters = [
            {
                Slug = "c-hills"
                RegionSlug = "hills"
                Name = "C hills"
            }
            {
                Slug = "c-coast"
                RegionSlug = "coast"
                Name = "C coast"
            }
            {
                Slug = "c-plains"
                RegionSlug = "plains"
                Name = "C plains"
            }
        ]
        Families = [ family "pine"; family "ocean"; family "stone" ]
        Interests = [
            for slug in
                [
                    "mountains"
                    "beaches"
                    "forts"
                    "food"
                    "yoga"
                    "wildlife"
                    "nightlife"
                    "faith"
                    "art"
                ] ->
                {
                    Slug = slug
                    Name = "Name of " + slug
                    ParentSlug = None
                }
        ]
        Places = [
            place "peak" "c-hills" "pine" "mountains"
            place "valley" "c-hills" "pine" "mountains"
            place "lake" "c-hills" "pine" "mountains"
            {
                place "party-beach" "c-coast" "ocean" "beaches" with
                    Months = months [ 12 ] [ 6; 7; 8 ]
            }
            {
                place "quiet-beach" "c-coast" "ocean" "beaches" with
                    Months = months [ 7 ] []
            }
            place "fort" "c-plains" "stone" "forts"
            place "market" "c-plains" "stone" "food"
        ]
        Links =
            both "peak" "valley"
            @ both "valley" "lake"
            @ both "party-beach" "quiet-beach"
            @ both "fort" "market"
            @ both "lake" "fort"
            @ both "market" "quiet-beach"
        Cards = [
            card "likes-hills" "interest:mountains" None
            card "likes-beach" "interest:beaches" None
            card "likes-forts" "interest:forts" None
            card "likes-food" "interest:food" None
            card "likes-yoga" "interest:yoga" None
            card "likes-wildlife" "interest:wildlife" None
            card "likes-nightlife" "interest:nightlife" None
            card "likes-faith" "interest:faith" None
            card "likes-art" "interest:art" None
            card "peak-card" "interest:mountains" (Some "peak")
            card "quiet-card" "interest:beaches" (Some "quiet-beach")
        ]
}

let liked cards = [ for slug in cards -> { Card = slug; Liked = true } ]

let request answers days month = {
    Answers = answers
    Days = days
    Month = month
    Travellers = TravellerType.Solo
    Budget = BudgetLevel.Mid
    TransportComfort = TransportComfort.Anything
}

let build catalog request =
    match Builder.build catalog request with
    | Ok trip -> trip
    | Error error -> raise (Xunit.Sdk.XunitException $"expected a trip, got {error}")

let slugs (trip: Trip) = trip.Stops |> List.map _.PlaceSlug

[<Theory>]
[<InlineData(5)>]
[<InlineData(7)>]
[<InlineData(10)>]
[<InlineData(14)>]
[<InlineData(21)>]
let ``days add up exactly`` days =
    let trip = build catalog (request (liked [ "likes-hills" ]) days None)
    Assert.Equal(days, trip.Stops |> List.sumBy _.Days)

[<Fact>]
let ``seven days stays in one region`` () =
    let trip = build catalog (request (liked [ "likes-hills"; "likes-beach" ]) 7 None)
    let families = trip.Stops |> List.map _.FamilySlug |> List.distinct
    Assert.Equal(1, families.Length)

[<Fact>]
let ``fourteen days can cover two regions`` () =
    let trip = build catalog (request (liked [ "likes-hills"; "likes-forts" ]) 14 None)
    let families = trip.Stops |> List.map _.FamilySlug |> List.distinct
    Assert.Equal(2, families.Length)

[<Fact>]
let ``unreachable region is left out rather than faked`` () =
    let island = {
        place "island" "c-coast" "ocean" "beaches" with
            Months = months [ 10 ] []
    }

    let world = {
        catalog with
            Places =
                island
                :: (catalog.Places |> List.filter (fun item -> item.ClusterSlug = "c-hills"))
            Links = both "peak" "valley" @ both "valley" "lake"
    }

    let trip = build world (request (liked [ "likes-hills"; "likes-beach" ]) 14 None)
    Assert.DoesNotContain("island", slugs trip)
    Assert.Equal(14, trip.Stops |> List.sumBy _.Days)

[<Fact>]
let ``monsoon beach lover gets swap with reason`` () =
    let trip = build catalog (request (liked [ "likes-beach" ]) 5 (Some 7))
    Assert.DoesNotContain("party-beach", slugs trip)
    Assert.Contains("quiet-beach", slugs trip)
    let swap = trip.Swaps.Head
    Assert.Equal("PARTY-BEACH", swap.From)
    Assert.Contains("July", swap.Reason)

[<Fact>]
let ``no swap when the favourite is in season`` () =
    let trip = build catalog (request (liked [ "likes-beach" ]) 5 (Some 12))
    Assert.Empty trip.Swaps

[<Fact>]
let ``closed place is never used in its closed month`` () =
    let closed = {
        place "only-beach" "c-coast" "ocean" "beaches" with
            Months = [
                {
                    Month = 1
                    Rating = Rating.Closed
                    Note = None
                    Evidence = evidence
                }
            ]
    }

    let trip =
        build
            {
                catalog with
                    Places = closed :: catalog.Places
            }
            (request (liked [ "likes-beach" ]) 5 (Some 1))

    Assert.DoesNotContain("only-beach", slugs trip)

[<Fact>]
let ``no month gives flexible trip with best months`` () =
    let trip = build catalog (request (liked [ "likes-hills" ]) 5 None)
    Assert.Equal<int list>([ 10; 11 ], trip.BestMonths)

[<Fact>]
let ``too many interests keeps strongest and lists rest`` () =
    let answers =
        liked [
            "likes-hills"
            "peak-card"
            "likes-beach"
            "likes-forts"
            "likes-food"
            "likes-yoga"
            "likes-wildlife"
            "likes-nightlife"
            "likes-faith"
            "likes-art"
        ]

    let trip = build catalog (request answers 5 None)
    Assert.Contains("PEAK", trip.Stops |> List.map _.Name)
    Assert.Contains("Name of yoga", trip.LeftOut)
    Assert.DoesNotContain("Name of mountains", trip.LeftOut)

[<Fact>]
let ``all passes gives gentle default`` () =
    let answers = [
        { Card = "likes-hills"; Liked = false }
        { Card = "likes-beach"; Liked = false }
    ]

    let trip = build catalog (request answers 7 None)
    Assert.NotEmpty trip.Stops
    Assert.Equal(7, trip.Stops |> List.sumBy _.Days)

[<Fact>]
let ``unknown card is ignored`` () =
    let trip = build catalog (request (liked [ "no-such-card"; "likes-hills" ]) 5 None)
    Assert.NotEmpty trip.Stops

[<Fact>]
let ``permit place is not added without being collected`` () =
    let gated = {
        place "permit-peak" "c-hills" "pine" "mountains" with
            Gate = Gate.Permit
            Popularity = Popularity.Offbeat
    }

    let trip =
        build
            {
                catalog with
                    Places = gated :: catalog.Places
            }
            (request (liked [ "likes-hills" ]) 5 None)

    Assert.DoesNotContain("permit-peak", slugs trip)

[<Fact>]
let ``permit stop carries permit notice`` () =
    let gated = {
        place "permit-peak" "c-hills" "pine" "mountains" with
            Gate = Gate.Permit
    }

    let collected = {
        card "permit-card" "interest:mountains" (Some "permit-peak") with
            Slug = "permit-card"
    }

    let world = {
        catalog with
            Places = gated :: catalog.Places
            Cards = collected :: catalog.Cards
            Links = both "permit-peak" "peak" @ catalog.Links
    }

    let trip = build world (request (liked [ "permit-card" ]) 5 None)
    Assert.Contains("permit-peak", slugs trip)

    Assert.Contains(
        trip.Notices,
        (fun notice -> notice.PlaceSlug = Some "permit-peak" && notice.Severity = NoticeSeverity.Permit)
    )

[<Fact>]
let ``place notices reach the trip`` () =
    let notice = {
        Slug = "n"
        Scope = NoticeScope.PlaceScope
        ScopeRef = Some "peak"
        Kind = "safety"
        Severity = Severity.Warn
        Body = "Mind the road."
        ValidFrom = None
        ValidTo = None
        Evidence = evidence
    }

    let trip =
        build { catalog with Notices = [ notice ] } (request (liked [ "peak-card" ]) 5 None)

    Assert.Contains(trip.Notices, (fun item -> item.Body = "Mind the road."))

[<Fact>]
let ``unverified notice is not shown`` () =
    let notice = {
        Slug = "n"
        Scope = NoticeScope.PlaceScope
        ScopeRef = Some "peak"
        Kind = "safety"
        Severity = Severity.Warn
        Body = "Rumour."
        ValidFrom = None
        ValidTo = None
        Evidence = {
            evidence with
                Status = FactStatus.Unverified
        }
    }

    let trip =
        build { catalog with Notices = [ notice ] } (request (liked [ "peak-card" ]) 5 None)

    Assert.DoesNotContain(trip.Notices, (fun item -> item.Body = "Rumour."))

[<Fact>]
let ``excluded regions never appear`` () =
    let blocked = place "blocked-valley" "c-hills" "pine" "mountains"

    let world = {
        catalog with
            Places = blocked :: catalog.Places
            Exclusions = [
                {
                    Slug = "blocked-valley"
                    Name = "Blocked"
                    Reason = "Advisory."
                }
            ]
            Links = both "blocked-valley" "peak" @ catalog.Links
    }

    let trip = build world (request (liked [ "likes-hills" ]) 14 None)
    Assert.DoesNotContain("blocked-valley", slugs trip)

[<Fact>]
let ``unpublished place never appears`` () =
    let hidden = {
        place "hidden" "c-hills" "pine" "mountains" with
            Published = false
    }

    let trip =
        build
            {
                catalog with
                    Places = hidden :: catalog.Places
            }
            (request (liked [ "likes-hills" ]) 14 None)

    Assert.DoesNotContain("hidden", slugs trip)

[<Fact>]
let ``no flights comfort uses no flights`` () =
    let world = {
        catalog with
            Links = flight "peak" "party-beach" :: flight "party-beach" "peak" :: catalog.Links
    }

    let answers = liked [ "peak-card"; "likes-beach" ]

    let trip =
        build world {
            request answers 14 None with
                TransportComfort = TransportComfort.NoFlights
        }

    let modes = trip.Stops |> List.choose _.Arrival |> List.map _.Mode
    Assert.DoesNotContain(TravelMode.Flight, modes)

[<Fact>]
let ``base family has most days`` () =
    let trip = build catalog (request (liked [ "likes-hills"; "likes-forts" ]) 14 None)

    let days family =
        trip.Stops
        |> List.filter (fun stop -> stop.FamilySlug = family)
        |> List.sumBy _.Days

    let others =
        trip.Stops
        |> List.map _.FamilySlug
        |> List.distinct
        |> List.filter (fun family -> family <> trip.BaseFamily)

    Assert.All(others, (fun family -> Assert.True(days family <= days trip.BaseFamily)))

[<Fact>]
let ``tie uses first collected`` () =
    let stop slug family : Builder.FamilyDays = {
        Slug = slug
        Name = slug
        Days = 3
        Family = family
    }

    let tied = [ stop "a" "stone"; stop "b" "pine" ]

    Assert.Equal("pine", Builder.baseFamily [ "b"; "a" ] tied)
    Assert.Equal("stone", Builder.baseFamily [ "a"; "b" ] tied)

[<Fact>]
let ``same input gives same trip`` () =
    let input = request (liked [ "likes-hills"; "likes-beach" ]) 10 (Some 11)
    Assert.Equal(build catalog input, build catalog input)

[<Fact>]
let ``consecutive stops are connected by a leg`` () =
    let trip = build catalog (request (liked [ "likes-hills" ]) 10 None)
    Assert.True(trip.Stops.Head.Arrival.IsNone)
    Assert.All(trip.Stops.Tail, (fun stop -> Assert.True stop.Arrival.IsSome))

[<Fact>]
let ``stay area follows budget`` () =
    let fancy = {
        place "peak" "c-hills" "pine" "mountains" with
            StayAreas = [
                {
                    Name = "Hostel row"
                    CostLevel = CostLevel.Budget
                    Note = None
                    Evidence = evidence
                }
                {
                    Name = "Palace road"
                    CostLevel = CostLevel.Luxury
                    Note = None
                    Evidence = evidence
                }
            ]
    }

    let world = { catalog with Places = [ fancy ] }

    let trip =
        build world {
            request (liked [ "likes-hills" ]) 5 None with
                Budget = BudgetLevel.Luxury
        }

    Assert.Equal(Some "Palace road", trip.Stops.Head.StayArea)

[<Fact>]
let ``every day of a stop has a plan`` () =
    let trip = build catalog (request (liked [ "likes-hills" ]) 7 None)
    Assert.All(trip.Stops, (fun stop -> Assert.Equal(stop.Days, stop.Plans.Length)))

[<Fact>]
let ``empty catalog is an error`` () =
    Assert.True((Builder.build Catalog.empty (request [] 7 None)).IsError)

[<Theory>]
[<InlineData(4)>]
[<InlineData(22)>]
let ``days outside 5 to 21 are an error`` days =
    Assert.True((Builder.build catalog (request [] days None)).IsError)

[<Fact>]
let ``depth lovers get fewer stops than breadth lovers`` () =
    let deep = card "slow" "depth" None

    let world = {
        catalog with
            Cards = deep :: catalog.Cards
    }

    let broad = build world (request (liked [ "likes-hills" ]) 7 None)
    let slow = build world (request (liked [ "likes-hills"; "slow" ]) 7 None)
    Assert.True(slow.Stops.Length < broad.Stops.Length)

[<Fact>]
let ``offbeat signal prefers offbeat places`` () =
    let hidden = {
        place "hidden-gem" "c-hills" "pine" "mountains" with
            Popularity = Popularity.Offbeat
    }

    let wild = card "wild" "offbeat" None

    let world = {
        catalog with
            Places = hidden :: catalog.Places
            Cards = wild :: catalog.Cards
            Links = both "hidden-gem" "peak" @ catalog.Links
    }

    let trip = build world (request (liked [ "wild"; "likes-hills" ]) 5 None)
    Assert.Equal("hidden-gem", trip.Stops.Head.PlaceSlug)

[<Theory>]
[<InlineData("solo")>]
[<InlineData("couple")>]
[<InlineData("friends")>]
[<InlineData("family")>]
let ``traveller fit shapes the first stop`` traveller =
    let kind, group =
        match traveller with
        | "solo" -> Traveller.Solo, TravellerType.Solo
        | "couple" -> Traveller.Couple, TravellerType.Couple
        | "friends" -> Traveller.Friends, TravellerType.Friends
        | _ -> Traveller.Family, TravellerType.Family

    let suited = {
        place "zz-suited" "c-hills" "pine" "mountains" with
            TravellerFit = [ { Traveller = kind; Fit = 3 } ]
    }

    let world = {
        catalog with
            Places = suited :: catalog.Places
            Links = both "zz-suited" "peak" @ catalog.Links
    }

    let trip =
        build world {
            request (liked [ "likes-hills" ]) 5 None with
                Travellers = group
        }

    Assert.Equal("zz-suited", trip.Stops.Head.PlaceSlug)

[<Fact>]
let ``place outside the budget ranks lower`` () =
    let pricey = {
        place "aa-pricey" "c-hills" "pine" "mountains" with
            CostLow = Some 3
            CostHigh = Some 3
    }

    let world = {
        catalog with
            Places = pricey :: catalog.Places
            Links = both "aa-pricey" "peak" @ catalog.Links
    }

    let trip =
        build world {
            request (liked [ "likes-hills" ]) 5 None with
                Budget = BudgetLevel.Budget
        }

    Assert.NotEqual<string>("aa-pricey", trip.Stops.Head.PlaceSlug)

[<Fact>]
let ``best season ranks above ordinary season`` () =
    let ripe = {
        place "zz-ripe" "c-hills" "pine" "mountains" with
            Months = months [ 3 ] []
    }

    let world = {
        catalog with
            Places = ripe :: catalog.Places
            Links = both "zz-ripe" "peak" @ catalog.Links
    }

    let trip = build world (request (liked [ "likes-hills" ]) 5 (Some 3))
    Assert.Equal("zz-ripe", trip.Stops.Head.PlaceSlug)

[<Fact>]
let ``unknown month ranks below ordinary season`` () =
    let vague = {
        place "aa-vague" "c-hills" "pine" "mountains" with
            Months = []
    }

    let world = {
        catalog with
            Places = vague :: catalog.Places
            Links = both "aa-vague" "peak" @ catalog.Links
    }

    let trip = build world (request (liked [ "likes-hills" ]) 5 (Some 3))
    Assert.NotEqual<string>("aa-vague", trip.Stops.Head.PlaceSlug)

[<Theory>]
[<InlineData("train")>]
[<InlineData("ferry")>]
[<InlineData("cable car")>]
[<InlineData("flight")>]
let ``leg names its transport mode`` name =
    let mode, expected =
        match name with
        | "train" -> Mode.Train, TravelMode.Train
        | "ferry" -> Mode.Ferry, TravelMode.Ferry
        | "cable car" -> Mode.CableCar, TravelMode.CableCar
        | _ -> Mode.Flight, TravelMode.Flight

    let links = [
        {
            road "peak" "valley" with
                Mode = mode
        }
        {
            road "valley" "peak" with
                Mode = mode
        }
    ]

    let world = {
        catalog with
            Places =
                catalog.Places
                |> List.filter (fun item -> item.Slug = "peak" || item.Slug = "valley")
            Links = links
    }

    let trip = build world (request (liked [ "likes-hills" ]) 7 None)
    let legs = trip.Stops |> List.choose _.Arrival
    Assert.Equal(expected, legs.Head.Mode)

[<Fact>]
let ``fastest link is chosen`` () =
    let slow = {
        road "peak" "valley" with
            MinutesMin = 600
            MinutesMax = 700
            Slug = "slow-road"
    }

    let world = {
        catalog with
            Links = slow :: catalog.Links
            Places =
                catalog.Places
                |> List.filter (fun item -> item.Slug = "peak" || item.Slug = "valley")
    }

    let trip = build world (request (liked [ "likes-hills" ]) 7 None)
    Assert.All(trip.Stops |> List.choose _.Arrival, (fun leg -> Assert.Equal(120, leg.MinutesMin)))

[<Fact>]
let ``unverified stay area is not offered`` () =
    let doubtful = {
        place "peak" "c-hills" "pine" "mountains" with
            StayAreas = [
                {
                    Name = "Rumoured"
                    CostLevel = CostLevel.Mid
                    Note = None
                    Evidence = {
                        evidence with
                            Status = FactStatus.Unverified
                    }
                }
            ]
    }

    let trip =
        build { catalog with Places = [ doubtful ] } (request (liked [ "likes-hills" ]) 5 None)

    Assert.Equal(None, trip.Stops.Head.StayArea)

[<Fact>]
let ``warn gate is carried to the stop`` () =
    let warned = {
        place "peak" "c-hills" "pine" "mountains" with
            Gate = Gate.Warn
    }

    let trip =
        build { catalog with Places = [ warned ] } (request (liked [ "likes-hills" ]) 5 None)

    Assert.Equal(PlaceAccess.Warning, trip.Stops.Head.Access)

[<Fact>]
let ``long stay past the maximum keeps days exact`` () =
    let only = place "peak" "c-hills" "pine" "mountains"

    let trip =
        build
            {
                catalog with
                    Places = [ only ]
                    Links = []
            }
            (request [] 21 None)

    Assert.Equal(21, trip.Stops.Head.Days)
    Assert.Equal(21, trip.Stops.Head.Plans.Length)

[<Fact>]
let ``place longer than the trip is trimmed to fit`` () =
    let long = {
        place "peak" "c-hills" "pine" "mountains" with
            DaysMin = 9
            DaysMax = 9
    }

    let trip =
        build
            {
                catalog with
                    Places = [ long ]
                    Links = []
            }
            (request [] 5 None)

    Assert.Equal(5, trip.Stops.Head.Days)

[<Fact>]
let ``title names the regions`` () =
    let trip = build catalog (request (liked [ "likes-hills" ]) 5 None)
    Assert.Equal("5 days in Hills", trip.Title)
