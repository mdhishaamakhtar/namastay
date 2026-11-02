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
    Shade = Shade.Deep
    Gate = Gate.Open
    Popularity = Popularity.Mainstream
    DaysMin = 2
    DaysMax = 4
    CostLow = Some 1
    CostHigh = Some 3
    Summary = "Summary."
    IsThisForYou = None
    GettingThere = None
    Airport = None
    Hub = false
    FoodIntro = None
    Dishes = []
    Eateries = []
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
    Landing = None
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
            {
                Slug = "hills"
                Name = "Hills"
                Colour = None
                Sort = None
            }
            {
                Slug = "coast"
                Name = "Coast"
                Colour = None
                Sort = None
            }
            {
                Slug = "plains"
                Name = "Plains"
                Colour = None
                Sort = None
            }
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
                    Colour = None
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
    Diet = None
    Scope = None
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
    Assert.Contains(trip.LeftOut, (fun item -> item.StartsWith "Name of"))
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
        Kind = NoticeKind.Safety
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
        Kind = NoticeKind.Safety
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
                Scope = None
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

[<Fact>]
let ``valid request has no problems`` () =
    Assert.Empty(RequestRules.check (request [] 7 (Some 3)))

[<Fact>]
let ``request rules report every problem`` () =
    let fields = RequestRules.check (request [] 3 (Some 13)) |> List.map _.Field
    Assert.Equal<string list>([ "days"; "month" ], fields)

[<Fact>]
let ``too many answers are reported`` () =
    let answers = [ for number in 1..1001 -> { Card = string number; Liked = true } ]
    Assert.Equal("answers", (RequestRules.check (request answers 7 None)).Head.Field)

[<Fact>]
let ``stop takes the first photo of its place`` () =
    let photo sort = {
        Slug = $"peak-{sort}"
        PlaceSlug = "peak"
        FilePath = $"photos/peak/{sort}.jpg"
        Alt = "A peak."
        Credit = "Someone"
        Licence = "CC0"
        SourceUrl = "https://example.org"
        Sort = sort
    }

    let world = {
        catalog with
            Photos = [ photo 2; photo 1 ]
    }

    let trip = build world (request (liked [ "peak-card" ]) 5 None)
    let stop = trip.Stops |> List.find (fun item -> item.PlaceSlug = "peak")
    Assert.Equal(Some "/media/photos/peak/1.jpg", stop.Photo |> Option.map _.Url)

[<Fact>]
let ``stop without a photo has none`` () =
    let trip = build catalog (request (liked [ "likes-hills" ]) 5 None)
    Assert.All(trip.Stops, (fun stop -> Assert.True stop.Photo.IsNone))

[<Fact>]
let ``mix shows the strongest interests as shares`` () =
    let trip =
        build catalog (request (liked [ "likes-hills"; "peak-card"; "likes-beach" ]) 5 None)

    Assert.Equal(
        {
            Name = "Name of mountains"
            Share = 100
        },
        trip.Mix.Head
    )

    Assert.Equal({ Name = "Name of beaches"; Share = 50 }, trip.Mix[1])

[<Fact>]
let ``mix is empty when nothing was liked`` () =
    Assert.Empty((build catalog (request [] 5 None)).Mix)

let private hubCatalog = {
    catalog with
        Places = [
            place "end" "c-hills" "pine" "mountains"
            place "hub" "c-hills" "pine" "food"
            place "next" "c-hills" "pine" "food"
            place "last" "c-hills" "pine" "food"
            place "strong-one" "c-coast" "ocean" "mountains"
            place "strong-two" "c-coast" "ocean" "mountains"
            place "strong-three" "c-coast" "ocean" "mountains"
        ]
        Links =
            both "end" "hub"
            @ both "hub" "next"
            @ both "next" "last"
            @ both "strong-one" "strong-two"
            @ both "strong-two" "strong-three"
}

let private hubCards = [ card "likes-end" "interest:mountains" (Some "end") ]

let private hubTrip days =
    build { hubCatalog with Cards = hubCards } (request (liked [ "likes-end" ]) days None)

[<Fact>]
let ``a liked place stays in the trip when another region scores higher`` () =
    let trip = hubTrip 7
    Assert.Contains("end", trip.Stops |> List.map _.PlaceSlug)

[<Fact>]
let ``a dead end does not stop the route`` () =
    let trip = hubTrip 12
    Assert.True(trip.Stops.Length >= 3)

[<Fact>]
let ``no stop runs past its maximum when a longer route exists`` () =
    let trip = hubTrip 12
    Assert.All(trip.Stops, (fun stop -> Assert.True(stop.Days <= 4)))

[<Fact>]
let ``no stop is cut below its minimum days`` () =
    for days in [ 7; 9; 11; 13; 14 ] do
        Assert.All((hubTrip days).Stops, (fun stop -> Assert.True(stop.Days >= 2)))

[<Fact>]
let ``a liked place survives a long route elsewhere`` () =
    Assert.Contains("end", (hubTrip 14).Stops |> List.map _.PlaceSlug)

[<Fact>]
let ``fourteen days does not hop every two days`` () =
    Assert.True((hubTrip 14).Stops.Length <= 5)

[<Fact>]
let ``a long fixed stay is not trimmed out of a short trip`` () =
    let fixedStay = {
        place "far" "c-hills" "pine" "mountains" with
            DaysMin = 7
            DaysMax = 7
    }

    let world = {
        catalog with
            Places = [ fixedStay; place "near" "c-hills" "pine" "mountains" ]
            Links = both "near" "far"
            Cards = [ card "likes-far" "interest:mountains" (Some "far") ]
    }

    let trip = build world (request (liked [ "likes-far" ]) 7 None)
    Assert.Equal<string list>([ "far" ], trip.Stops |> List.map _.PlaceSlug)

let private airport slug cluster family interest = {
    place slug cluster family interest with
        Airport = Some "Test International (TST)"
}

let private gatewayCatalog = {
    catalog with
        Places = [
            airport "capital" "c-plains" "stone" "forts"
            place "hub" "c-hills" "pine" "food"
            place "remote" "c-hills" "pine" "mountains"
        ]
        Links = both "capital" "hub" @ both "hub" "remote"
        Cards = [ card "likes-remote" "interest:mountains" (Some "remote") ]
}

let private gatewayTrip days =
    build gatewayCatalog (request (liked [ "likes-remote" ]) days None)

[<Fact>]
let ``a place with no airport link is reached through the nearest city`` () =
    let trip = gatewayTrip 6
    Assert.Equal<string list>([ "hub"; "remote"; "capital" ], trip.Stops |> List.map _.PlaceSlug)

[<Fact>]
let ``the trip says where you land and how you get to the first stop`` () =
    let arrive = (gatewayTrip 6).Arrive.Value
    Assert.Equal("CAPITAL", arrive.City)
    Assert.Equal("Test International (TST)", arrive.Airport)
    Assert.True arrive.Leg.IsSome

[<Fact>]
let ``starting in a gateway city needs no onward leg`` () =
    let world = {
        gatewayCatalog with
            Cards = [ card "likes-capital" "interest:forts" (Some "capital") ]
    }

    let trip = build world (request (liked [ "likes-capital" ]) 5 None)
    Assert.Equal("capital", trip.Stops.Head.PlaceSlug)
    Assert.True(trip.Arrive.Value.Leg.IsNone)

[<Fact>]
let ``the trip says where you fly home from`` () =
    let depart = (gatewayTrip 6).Depart.Value
    Assert.Equal("CAPITAL", depart.City)
    Assert.Equal(None, depart.Via)

[<Fact>]
let ``no gateways in the data means no arrival is claimed`` () = Assert.True((hubTrip 7).Arrive.IsNone)

[<Fact>]
let ``a trip that includes the landing city stays there last`` () =
    let world = {
        gatewayCatalog with
            Places = [
                airport "capital" "c-hills" "stone" "forts"
                place "side" "c-hills" "pine" "mountains"
            ]
            Links = both "capital" "side"
            Cards = [ card "likes-side" "interest:mountains" (Some "side") ]
    }

    let trip = build world (request (liked [ "likes-side" ]) 6 None)
    Assert.Equal<string list>([ "side"; "capital" ], trip.Stops |> List.map _.PlaceSlug)

let private hubAirport slug cluster family interest = {
    airport slug cluster family interest with
        Hub = true
}

let private landing places links =
    let world = {
        catalog with
            Places = places
            Links = links
            Cards = [ card "likes-remote" "interest:mountains" (Some "remote") ]
    }

    (build world (request (liked [ "likes-remote" ]) 6 None)).Arrive.Value

let private timed from target minutes =
    both from target
    |> List.map (fun link -> {
        link with
            MinutesMin = minutes
            MinutesMax = minutes
    })

[<Fact>]
let ``a main hub beats a slightly closer smaller airport`` () =
    let arrive =
        landing
            [
                hubAirport "main" "c-plains" "stone" "forts"
                airport "minor" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (timed "main" "remote" 135 @ timed "minor" "remote" 80)

    Assert.Equal("MAIN", arrive.City)

[<Fact>]
let ``a smaller airport wins when it alone reaches the place directly`` () =
    let arrive =
        landing
            [
                hubAirport "main" "c-plains" "stone" "forts"
                airport "minor" "c-plains" "stone" "forts"
                place "between" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (timed "main" "between" 60
             @ timed "between" "remote" 60
             @ timed "minor" "remote" 300)

    Assert.Equal("MINOR", arrive.City)

[<Fact>]
let ``the nearest of two hubs wins`` () =
    let arrive =
        landing
            [
                hubAirport "far-hub" "c-plains" "stone" "forts"
                hubAirport "near-hub" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (timed "far-hub" "remote" 145 @ timed "near-hub" "remote" 70)

    Assert.Equal("NEAR-HUB", arrive.City)

[<Fact>]
let ``a link stored one way still brings you home`` () =
    let world = {
        catalog with
            Places = [
                hubAirport "main" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            Links = [ road "main" "remote" ]
            Cards = [ card "likes-remote" "interest:mountains" (Some "remote") ]
    }

    let trip = build world (request (liked [ "likes-remote" ]) 6 None)
    Assert.Equal("MAIN", trip.Depart.Value.City)

[<Fact>]
let ``an airport city that isn't a stop can still be where you land`` () =
    let arrive =
        landing
            [
                {
                    airport "gateway" "c-plains" "stone" "forts" with
                        Published = false
                }
                place "remote" "c-hills" "pine" "mountains"
            ]
            (both "gateway" "remote")

    Assert.Equal("GATEWAY", arrive.City)

[<Fact>]
let ``a gateway city goes by its plain name`` () =
    let arrive =
        landing
            [
                {
                    hubAirport "main" "c-plains" "stone" "forts" with
                        Name = "Delhi with Gurugram"
                }
                place "remote" "c-hills" "pine" "mountains"
            ]
            (both "main" "remote")

    Assert.Equal("Delhi", arrive.City)

[<Fact>]
let ``the way home takes the quicker direction of a route`` () =
    let world = {
        catalog with
            Places = [
                hubAirport "main" "c-plains" "stone" "forts"
                hubAirport "other" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            Links =
                timed "main" "remote" 100 @ timed "other" "remote" 150
                |> List.filter (fun link -> not (link.FromSlug = "remote" && link.ToSlug = "main"))
                |> List.append [
                    {
                        road "remote" "main" with
                            MinutesMin = 1600
                            MinutesMax = 1600
                    }
                ]
            Cards = [ card "likes-remote" "interest:mountains" (Some "remote") ]
    }

    let trip = build world (request (liked [ "likes-remote" ]) 6 None)
    Assert.Equal("MAIN", trip.Depart.Value.City)

[<Fact>]
let ``a liked place stays in the trip when a stronger taste pulls elsewhere`` () =
    let world = {
        catalog with
            Places = [
                place "liked" "c-hills" "pine" "mountains"
                place "feast-one" "c-plains" "stone" "food"
                place "feast-two" "c-plains" "stone" "food"
                place "feast-three" "c-plains" "stone" "food"
            ]
            Links = both "feast-one" "feast-two" @ both "feast-two" "feast-three"
            Cards = [
                card "likes-place" "interest:mountains" (Some "liked")
                card "dish-one" "interest:food" (Some "liked")
                card "dish-two" "interest:food" (Some "liked")
            ]
    }

    let trip =
        build world (request (liked [ "likes-place"; "dish-one"; "dish-two" ]) 7 None)

    Assert.Contains("liked", trip.Stops |> List.map _.PlaceSlug)

[<Fact>]
let ``a short trip can cross a region for a place someone liked`` () =
    let world = {
        catalog with
            Places = [
                place "fort-city" "c-plains" "stone" "forts"
                place "hill-city" "c-hills" "pine" "mountains"
            ]
            Links = both "fort-city" "hill-city"
            Cards = [
                card "likes-fort" "interest:forts" (Some "fort-city")
                card "likes-hill" "interest:mountains" (Some "hill-city")
            ]
    }

    let trip = build world (request (liked [ "likes-fort"; "likes-hill" ]) 6 None)
    Assert.Contains("hill-city", trip.Stops |> List.map _.PlaceSlug)
    Assert.Contains("fort-city", trip.Stops |> List.map _.PlaceSlug)

[<Fact>]
let ``a place nobody liked still can't stretch a short trip across regions`` () =
    let world = {
        catalog with
            Places = [
                place "fort-city" "c-plains" "stone" "forts"
                place "hill-city" "c-hills" "pine" "mountains"
            ]
            Links = both "fort-city" "hill-city"
            Cards = [ card "likes-fort" "interest:forts" (Some "fort-city") ]
    }

    let trip = build world (request (liked [ "likes-fort" ]) 6 None)
    Assert.DoesNotContain("hill-city", trip.Stops |> List.map _.PlaceSlug)

let private hardMonth rating = {
    catalog with
        Places = [
            {
                place "chosen" "c-hills" "pine" "mountains" with
                    Months = [
                        {
                            Month = 6
                            Rating = rating
                            Note = Some "Very hot by day."
                            Evidence = evidence
                        }
                    ]
            }
            place "fallback" "c-plains" "stone" "forts"
        ]
        Links = []
        Cards = [ card "likes-chosen" "interest:mountains" (Some "chosen") ]
}

let private hardMonthTrip rating =
    build (hardMonth rating) (request (liked [ "likes-chosen" ]) 6 (Some 6))

[<Fact>]
let ``a liked place stays in a hard month and the trip says why it's hard`` () =
    let trip = hardMonthTrip Rating.Avoid
    Assert.Equal<string list>([ "chosen" ], trip.Stops |> List.map _.PlaceSlug)

    let warning =
        trip.Notices |> List.find (fun notice -> notice.Kind = GuideNoticeKind.Season)

    Assert.Contains("Very hot by day.", warning.Body)
    Assert.Contains("June", warning.Body)

[<Fact>]
let ``a liked place that is closed that month is still left out`` () =
    let trip = hardMonthTrip Rating.Closed
    Assert.DoesNotContain("chosen", trip.Stops |> List.map _.PlaceSlug)

[<Fact>]
let ``a long journey makes the first day a travel day`` () =
    let world = {
        catalog with
            Places = [
                place "near" "c-hills" "pine" "mountains"
                place "far" "c-hills" "pine" "mountains"
            ]
            Links = timed "near" "far" 360
            Cards = [
                card "likes-near" "interest:mountains" (Some "near")
                card "likes-far" "interest:mountains" (Some "far")
            ]
    }

    let trip = build world (request (liked [ "likes-near"; "likes-far" ]) 6 None)
    Assert.Equal(Builder.travelDay, trip.Stops[1].Plans.Head.Plan)

[<Fact>]
let ``a liked place that couldn't fit is named`` () =
    let world = {
        catalog with
            Places = [
                place "first" "c-hills" "pine" "mountains"
                place "island" "c-plains" "stone" "forts"
            ]
            Links = []
            Cards = [
                card "likes-first" "interest:mountains" (Some "first")
                card "likes-first-again" "interest:mountains" (Some "first")
                card "likes-island" "interest:forts" (Some "island")
            ]
    }

    let trip =
        build world (request (liked [ "likes-first"; "likes-first-again"; "likes-island" ]) 6 None)

    Assert.Contains("ISLAND", trip.LeftOut @ [ trip.Stops.Head.Name ])

[<Fact>]
let ``a travel day picks the plan that fits the days left`` () =
    let plan variant day text : DayPlan = {
        Variant = variant
        Day = day
        Plan = text
        Evidence = evidence
    }

    let world = {
        catalog with
            Places = [
                place "near" "c-hills" "pine" "mountains"
                {
                    place "far" "c-hills" "pine" "mountains" with
                        DayPlans = [
                            plan "short" 1 "short one"
                            plan "short" 2 "short two"
                            plan "long" 1 "long one"
                            plan "long" 2 "long two"
                            plan "long" 3 "long three"
                        ]
                }
            ]
            Links = timed "near" "far" 360
            Cards = [
                card "likes-near" "interest:mountains" (Some "near")
                card "likes-far" "interest:mountains" (Some "far")
            ]
    }

    let trip = build world (request (liked [ "likes-near"; "likes-far" ]) 6 None)
    let stop = trip.Stops |> List.find (fun item -> item.PlaceSlug = "far")
    Assert.Contains("short two", stop.Plans |> List.map _.Plan)

[<Fact>]
let ``a limited trip only uses the allowed places`` () =
    let world = {
        catalog with
            Places = [
                place "inside" "c-hills" "pine" "mountains"
                place "outside" "c-hills" "pine" "mountains"
            ]
            Links = both "inside" "outside"
            Cards = [ card "likes-outside" "interest:mountains" (Some "outside") ]
    }

    let result =
        Builder.buildWithin (Some(Set.ofList [ "inside" ])) world (request (liked [ "likes-outside" ]) 6 None)

    match result with
    | Ok trip -> Assert.Equal<string list>([ "inside" ], trip.Stops |> List.map _.PlaceSlug)
    | Error error -> raise (Xunit.Sdk.XunitException $"expected a trip, got {error}")

let private likingRemote places links days month =
    let world = {
        catalog with
            Places = places
            Links = links
            Cards = [ card "likes-remote" "interest:mountains" (Some "remote") ]
    }

    build world (request (liked [ "likes-remote" ]) days month)

[<Fact>]
let ``a trip ends with a stay in a major city`` () =
    let trip =
        likingRemote
            [
                hubAirport "main" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (both "main" "remote")
            6
            None

    Assert.Equal("main", (List.last trip.Stops).PlaceSlug)
    Assert.Equal("MAIN", trip.Depart.Value.City)
    Assert.Equal(None, trip.Depart.Value.Leg)

[<Fact>]
let ``a city a trip starts in moves to the end instead of appearing twice`` () =
    let trip =
        likingRemote
            [
                hubAirport "main" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (both "main" "remote")
            6
            None

    Assert.Equal<string list>([ "remote"; "main" ], slugs trip)
    Assert.Equal(None, trip.Stops.Head.Arrival)
    Assert.Equal("MAIN", trip.Arrive.Value.City)

[<Theory>]
[<InlineData(7, 1)>]
[<InlineData(8, 2)>]
[<InlineData(14, 2)>]
let ``the closing city gets a day or two and the days still add up`` days closing =
    let trip =
        likingRemote
            [
                hubAirport "main" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            (both "main" "remote")
            days
            None

    Assert.Equal(closing, (List.last trip.Stops).Days)
    Assert.Equal(days, trip.Stops |> List.sumBy _.Days)

[<Fact>]
let ``a smaller airport hands over to a major city nearby`` () =
    let trip =
        likingRemote
            [
                hubAirport "main" "c-plains" "stone" "forts"
                {
                    airport "remote" "c-hills" "pine" "mountains" with
                        DaysMin = 2
                }
            ]
            (both "main" "remote")
            6
            None

    Assert.Equal("main", (List.last trip.Stops).PlaceSlug)

[<Fact>]
let ``a smaller airport can end a trip when no major city is in reach`` () =
    let trip = likingRemote [ airport "remote" "c-hills" "pine" "mountains" ] [] 6 None

    Assert.Equal<string list>([ "remote" ], slugs trip)
    Assert.Equal("REMOTE", trip.Depart.Value.City)

[<Fact>]
let ``you can still end in a major city in its hard month`` () =
    let trip =
        likingRemote
            [
                {
                    hubAirport "main" "c-plains" "stone" "forts" with
                        Months = months [] [ 3 ]
                }
                {
                    place "remote" "c-hills" "pine" "mountains" with
                        Months = months [ 3 ] []
                }
            ]
            (both "main" "remote")
            6
            (Some 3)

    Assert.Equal("main", (List.last trip.Stops).PlaceSlug)

[<Fact>]
let ``a trip kept to one area doesn't leave it to end`` () =
    let world = {
        catalog with
            Places = [
                hubAirport "main" "c-plains" "stone" "forts"
                place "remote" "c-hills" "pine" "mountains"
            ]
            Links = both "main" "remote"
            Cards = [ card "likes-remote" "interest:mountains" (Some "remote") ]
    }

    match Builder.buildWithin (Some(Set.singleton "remote")) world (request (liked [ "likes-remote" ]) 6 None) with
    | Ok trip ->
        Assert.Equal<string list>([ "remote" ], slugs trip)
        Assert.Equal("MAIN", trip.Depart.Value.City)
    | Error error -> raise (Xunit.Sdk.XunitException $"expected a trip, got {error}")

[<Fact>]
let ``a major city sits at the start or end and not in the middle`` () =
    let world = {
        catalog with
            Places = [
                place "north" "c-hills" "pine" "mountains"
                hubAirport "main" "c-plains" "stone" "forts"
                place "south" "c-plains" "stone" "mountains"
            ]
            Links = both "north" "main" @ both "main" "south" @ both "north" "south"
            Cards = [
                card "likes-north" "interest:mountains" (Some "north")
                card "likes-south" "interest:mountains" (Some "south")
            ]
    }

    let trip = build world (request (liked [ "likes-north"; "likes-south" ]) 9 None)
    Assert.Equal("main", (List.last trip.Stops).PlaceSlug)
    Assert.Equal(3, trip.Stops.Length)

[<Fact>]
let ``a place one city away from a hub still ends with a stay there`` () =
    let trip =
        likingRemote
            [
                hubAirport "main" "c-plains" "stone" "forts"
                {
                    place "between" "c-plains" "stone" "forts" with
                        Published = false
                }
                place "remote" "c-hills" "pine" "mountains"
            ]
            (timed "main" "between" 200 @ timed "between" "remote" 120)
            6
            None

    let last = List.last trip.Stops
    Assert.Equal("main", last.PlaceSlug)
    Assert.Equal(320, last.Arrival.Value.MinutesMin)
    Assert.Equal(Some "Through BETWEEN.", last.Arrival.Value.ComfortNote)

[<Fact>]
let ``a major city passed through mid-trip becomes the last stay`` () =
    let world = {
        catalog with
            Places = [
                place "north" "c-hills" "pine" "mountains"
                hubAirport "main" "c-plains" "stone" "forts"
                place "south" "c-plains" "stone" "mountains"
            ]
            Links = timed "north" "main" 100 @ timed "main" "south" 200
            Cards = [
                card "likes-north" "interest:mountains" (Some "north")
                card "likes-south" "interest:mountains" (Some "south")
            ]
    }

    let trip = build world (request (liked [ "likes-north"; "likes-south" ]) 9 None)
    Assert.Equal<string list>([ "main"; "north"; "south" ], slugs trip |> List.sort)
    Assert.Equal("main", (List.last trip.Stops).PlaceSlug)
    Assert.Equal(300, trip.Stops[1].Arrival.Value.MinutesMin)
    Assert.Equal(Some "Through MAIN.", trip.Stops[1].Arrival.Value.ComfortNote)

[<Fact>]
let ``a closer airport city with no guide is flown from directly`` () =
    let trip =
        likingRemote
            [
                hubAirport "main" "c-plains" "stone" "forts"
                {
                    hubAirport "near" "c-plains" "stone" "forts" with
                        Published = false
                }
                place "remote" "c-hills" "pine" "mountains"
            ]
            (timed "main" "remote" 300 @ timed "near" "remote" 60)
            6
            None

    Assert.Equal("NEAR", trip.Depart.Value.City)
    Assert.DoesNotContain("near", slugs trip)

[<Fact>]
let ``a short trip that can't fit the stop after a major city still ends there`` () =
    let world = {
        catalog with
            Places = [
                place "north" "c-hills" "pine" "mountains"
                hubAirport "main" "c-plains" "stone" "forts"
                place "south" "c-plains" "stone" "mountains"
            ]
            Links = timed "north" "main" 100 @ timed "main" "south" 200
            Cards = [
                card "likes-north" "interest:mountains" (Some "north")
                card "likes-south" "interest:mountains" (Some "south")
            ]
    }

    let trip = build world (request (liked [ "likes-north"; "likes-south" ]) 6 None)
    Assert.Equal("main", (List.last trip.Stops).PlaceSlug)
    Assert.Equal(6, trip.Stops |> List.sumBy _.Days)

let private dish name vegetarian vegan rank : Dish = {
    Name = name
    Description = "Description."
    Vegetarian = vegetarian
    Vegan = vegan
    PhotoSlug = None
    Rank = rank
    Evidence = evidence
}

let private eatery name vegetarianOnly : Eatery = {
    Name = name
    Area = "Old town"
    GoFor = "Lunch."
    CostLevel = CostLevel.Budget
    VegetarianOnly = vegetarianOnly
    Evidence = evidence
}

let private foodWorld dishes eateries = {
    catalog with
        Places = [
            {
                place "fed" "c-hills" "pine" "mountains" with
                    Dishes = dishes
                    Eateries = eateries
            }
        ]
        Links = []
        Cards = [ card "likes-fed" "interest:mountains" (Some "fed") ]
}

let private foodFor diet dishes eateries =
    let asked = {
        request (liked [ "likes-fed" ]) 5 None with
            Diet = diet
    }

    (build (foodWorld dishes eateries) asked).Stops.Head.Food

let private menu = [
    dish "Mutton" false false (Some 1)
    dish "Paneer" true false (Some 2)
    dish "Dal" true true (Some 3)
    dish "Chana" true true None
]

let private kitchens = [ eatery "Grill" false; eatery "Thali house" true ]

let private dishNames (food: StopFood option) = food.Value.Dishes |> List.map _.Name

[<Fact>]
let ``every trip picks out the best-known dishes and says what they are`` () =
    let food = foodFor None menu kitchens
    Assert.Equal<string list>([ "Mutton"; "Paneer"; "Dal" ], dishNames food)

    Assert.Equal<DishKind list>(
        [ DishKind.NonVeg; DishKind.Vegetarian; DishKind.Vegan ],
        food.Value.Dishes |> List.map _.Kind
    )

    Assert.Equal(2, food.Value.Eateries.Length)

[<Fact>]
let ``a vegetarian trip leaves out meat and mixed kitchens`` () =
    let food = foodFor (Some Diet.Vegetarian) menu kitchens
    Assert.Equal<string list>([ "Paneer"; "Dal"; "Chana" ], dishNames food)
    Assert.Equal<string list>([ "Thali house" ], food.Value.Eateries |> List.map _.Name)

[<Fact>]
let ``a vegan trip only picks vegan dishes`` () =
    let food = foodFor (Some Diet.Vegan) menu kitchens
    Assert.Equal<string list>([ "Dal"; "Chana" ], dishNames food)

[<Fact>]
let ``a stop with nothing for a diet still has a food row to give advice`` () =
    let food = foodFor (Some Diet.Vegan) [ dish "Mutton" false false None ] []
    Assert.True(food.IsSome)
    Assert.Empty food.Value.Dishes

[<Fact>]
let ``a place with no food data has no food row`` () =
    Assert.True((foodFor None [] []).IsNone)

[<Fact>]
let ``the diet is kept on the trip`` () =
    let asked = {
        request (liked [ "likes-fed" ]) 5 None with
            Diet = Some Diet.Vegan
    }

    Assert.Equal(Some Diet.Vegan, (build (foodWorld menu kitchens) asked).Diet)
