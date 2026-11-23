module Namastay.UnitTests.RouteSketchTests

open Xunit
open Namastay.Shared
open Namastay.Shared.Contracts

let private box: RouteSketch.Box = {
    Width = 300.0
    Height = 200.0
    Margin = 20.0
}

let private point latitude longitude : MapPoint = {
    Latitude = latitude
    Longitude = longitude
}

let private delhi = point 28.6 77.2
let private mumbai = point 19.1 72.9
let private kolkata = point 22.6 88.4
let private chennai = point 13.1 80.3
let private south = [ point 12.6 80.2; point 15.3 76.5; point 17.4 78.5 ]

let private view points =
    match RouteSketch.fit box points with
    | Some found -> found
    | None -> failwith "expected a view"

let private placed points =
    points |> List.map (RouteSketch.toBox (view points))

[<Fact>]
let ``north is up and east is right`` () =
    match placed [ delhi; mumbai; kolkata ] with
    | [ (delhiX, delhiY); (mumbaiX, mumbaiY); (kolkataX, kolkataY) ] ->
        Assert.True(delhiY < mumbaiY, "Delhi is north of Mumbai")
        Assert.True(delhiY < kolkataY, "Delhi is north of Kolkata")
        Assert.True(mumbaiX < delhiX && delhiX < kolkataX, "Mumbai, Delhi, Kolkata run west to east")
    | other -> Assert.Fail $"expected three points, got {other}"

[<Fact>]
let ``every stop stays inside the margin`` () =
    for x, y in placed [ delhi; mumbai; kolkata ] do
        Assert.InRange(x, 20.0, 280.0)
        Assert.InRange(y, 20.0, 180.0)

[<Fact>]
let ``distances keep their proportions`` () =
    match placed [ point 10.0 80.0; point 20.0 80.0; point 30.0 80.0 ] with
    | [ (_, southY); (_, middleY); (_, northY) ] -> Assert.Equal(southY - middleY, middleY - northY, 6)
    | other -> Assert.Fail $"expected three points, got {other}"

[<Fact>]
let ``a long route fills its box and sits in the middle`` () =
    match placed [ point 10.0 80.0; point 30.0 80.0 ] with
    | [ (southX, southY); (northX, northY) ] ->
        Assert.Equal(150.0, southX, 6)
        Assert.Equal(150.0, northX, 6)
        Assert.Equal(180.0, southY, 6)
        Assert.Equal(20.0, northY, 6)
    | other -> Assert.Fail $"expected two points, got {other}"

[<Fact>]
let ``stops close together keep the country around them`` () =
    match placed [ point 10.0 76.0; point 11.0 76.0 ] with
    | [ (_, southY); (_, northY) ] -> Assert.Equal(32.0, southY - northY, 6)
    | other -> Assert.Fail $"expected two points, got {other}"

[<Fact>]
let ``one stop alone sits in the middle`` () =
    Assert.Equal<(float * float) list>([ 150.0, 100.0 ], placed [ delhi ])

[<Fact>]
let ``no stops make no drawing`` () =
    Assert.Equal(None, RouteSketch.fit box [])

[<Fact>]
let ``a main city beside the route is in the picture`` () =
    Assert.True(RouteSketch.shows box (view south) chennai)

[<Fact>]
let ``a main city far from the route is out of the picture`` () =
    Assert.False(RouteSketch.shows box (view south) delhi)
    Assert.False(RouteSketch.shows box (view south) kolkata)

[<Fact>]
let ``the ground a view covers comes back as its corners`` () =
    let southWest, northEast =
        RouteSketch.corners box (view [ point 10.0 80.0; point 30.0 80.0 ])

    Assert.Equal((0.0, 200.0), RouteSketch.toBox (view [ point 10.0 80.0; point 30.0 80.0 ]) southWest)
    Assert.Equal((300.0, 0.0), RouteSketch.toBox (view [ point 10.0 80.0; point 30.0 80.0 ]) northEast)

let private apart (x1: float, y1: float) (x2: float, y2: float) =
    sqrt ((x1 - x2) ** 2.0 + (y1 - y2) ** 2.0)

let private pairs (points: (float * float) list) = [
    for index, first in List.indexed points do
        for second in List.skip (index + 1) points -> first, second
]

[<Fact>]
let ``dots that would cover each other are eased apart`` () =
    let eased =
        RouteSketch.easeApart 20.0 [ 100.0, 100.0; 104.0, 100.0; 100.0, 103.0; 102.0, 98.0 ]

    for first, second in pairs eased do
        Assert.True(apart first second >= 19.0, $"{first} and {second} still overlap")

[<Fact>]
let ``dots in the very same place are still told apart`` () =
    match RouteSketch.easeApart 20.0 [ 50.0, 50.0; 50.0, 50.0 ] with
    | [ first; second ] -> Assert.True(apart first second >= 19.0)
    | other -> Assert.Fail $"expected two points, got {other}"

[<Fact>]
let ``dots with room between them stay where they are`` () =
    let spread = [ 10.0, 10.0; 100.0, 40.0; 200.0, 150.0 ]
    Assert.Equal<(float * float) list>(spread, RouteSketch.easeApart 20.0 spread)

[<Fact>]
let ``eased dots stay near where they belong`` () =
    let crowded = [ 100.0, 100.0; 104.0, 100.0; 100.0, 103.0 ]

    for before, after in List.zip crowded (RouteSketch.easeApart 20.0 crowded) do
        Assert.True(apart before after <= 20.0, $"{before} moved to {after}")
