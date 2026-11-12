module Namastay.UnitTests.Trip.ReasonsTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Domain.Trip
open Namastay.Domain.Trip.Routing
open Namastay.Shared.Contracts
open Namastay.UnitTests.Trip.TripBuilderTests

let private at (slug: string) = {
    Place = catalog.Places |> List.find (fun place -> place.Slug = slug)
    Days = 2
    Arrival = None
    Closing = false
}

let private closingAt slug = { at slug with Closing = true }

/// The reasons a traveller who liked these cards gets for a route.
let private reasonsIn (world: Catalog) (asked: TripRequest) (stops: RoutedStop list) =
    Reasons.forStops world (Profile.from world asked) asked stops

let private reasons (cards: string list) (stops: RoutedStop list) =
    reasonsIn catalog (request (liked cards) 7 None) stops

let private withCards (cards: Card list) = {
    catalog with
        Cards = catalog.Cards @ cards
}

[<Fact>]
let ``a stop names the card liked there`` () =
    Assert.Equal<string option list>([ Some "Because you liked “peak-card”" ], reasons [ "peak-card" ] [ at "peak" ])

[<Fact>]
let ``a stop names two liked cards and no more`` () =
    let world =
        withCards [
            for index in 1..3 -> card $"view-{index}" "interest:mountains" (Some "valley")
        ]

    let asked = request (liked [ "view-2"; "view-1"; "view-3" ]) 7 None

    Assert.Equal<string option list>(
        [ Some "Because you liked “view-2” and “view-1”" ],
        reasonsIn world asked [ at "valley" ]
    )

[<Fact>]
let ``the city added for the flight home says so`` () =
    let found = reasons [ "peak-card" ] [ at "peak"; closingAt "market" ]
    Assert.Equal(Some Reasons.closing, found[1])

[<Fact>]
let ``a liked city that also closes the trip is there because it was liked`` () =
    let found = reasons [ "peak-card" ] [ at "valley"; closingAt "peak" ]
    Assert.Equal(Some "Because you liked “peak-card”", found[1])

[<Fact>]
let ``an interest liked more than once was kept choosing`` () =
    let found = reasons [ "likes-hills"; "peak-card" ] [ at "valley" ]
    Assert.Equal<string option list>([ Some "Name of mountains, which you kept choosing" ], found)

[<Fact>]
let ``an interest liked once was liked`` () =
    let found = reasons [ "likes-hills" ] [ at "valley" ]
    Assert.Equal<string option list>([ Some "Name of mountains, which you liked" ], found)

[<Fact>]
let ``a place answers the child interest the traveller liked`` () =
    let world = {
        withCards [ card "likes-snow" "interest:snow-peaks" None ] with
            Interests =
                {
                    Slug = "snow-peaks"
                    Name = "Snow peaks"
                    ParentSlug = Some "mountains"
                    Colour = None
                }
                :: catalog.Interests
    }

    let found =
        reasonsIn world (request (liked [ "likes-snow" ]) 7 None) [ at "valley" ]

    Assert.Equal<string option list>([ Some "Snow peaks, which you liked" ], found)

[<Fact>]
let ``a trip planned from a feeling says so at its stops`` () =
    let asked = {
        request [] 7 None with
            Scope =
                Some {
                    Kind = ScopeKind.Feeling
                    Slugs = [ "mountains" ]
                }
    }

    Assert.Equal<string option list>(
        [ Some "Name of mountains, which this trip is planned around" ],
        reasonsIn catalog asked [ at "valley" ]
    )

[<Fact>]
let ``a place only weakly about an interest is not there for it`` () =
    let weak = {
        at "valley" with
            Place = {
                place "valley" "c-hills" "pine" "mountains" with
                    Interests = [
                        {
                            InterestSlug = "mountains"
                            Weight = 2
                        }
                    ]
            }
    }

    Assert.Equal<string option list>([ None ], reasons [ "likes-hills" ] [ weak ])

[<Fact>]
let ``a stop with no reason of its own is on the way to the next`` () =
    let found = reasons [ "peak-card" ] [ at "market"; at "peak" ]
    Assert.Equal<string option list>([ Some "On the way to PEAK"; Some "Because you liked “peak-card”" ], found)

[<Fact>]
let ``the city to fly home from is not somewhere to be on the way to`` () =
    let found = reasons [ "peak-card" ] [ at "peak"; at "market"; closingAt "fort" ]
    Assert.Equal(None, found[1])

[<Fact>]
let ``a last stop with nothing to say has no reason`` () =
    Assert.Equal<string option list>([ None ], reasons [] [ at "market" ])

[<Fact>]
let ``a built trip carries a reason at the stop that was liked`` () =
    let trip = build catalog (request (liked [ "peak-card" ]) 7 None)
    let peak = trip.Stops |> List.find (fun stop -> stop.PlaceSlug = "peak")
    Assert.Equal(Some "Because you liked “peak-card”", peak.Reason)
