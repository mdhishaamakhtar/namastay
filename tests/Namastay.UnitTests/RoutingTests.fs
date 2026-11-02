module Namastay.UnitTests.RoutingTests

open System
open Xunit
open FsCheck
open FsCheck.Xunit
open Namastay.Shared

[<Fact>]
let ``url fills path values`` () =
    let route: Route<NoBody, string> = Route.get "/api/places/{slug}"
    Assert.Equal("/api/places/goa", Route.url [ "slug", "goa" ] [] route)

[<Fact>]
let ``url escapes query values`` () =
    let route: Route<NoBody, string> = Route.get "/api/places"
    Assert.Equal("/api/places?query=a%20b%26c", Route.url [] [ "query", "a b&c" ] route)

[<Fact>]
let ``matches finds a page with no values`` () =
    let route: Route<NoBody, string> = Route.get "/explore"
    Assert.Equal(Some [], Route.matches route [ "explore" ])

[<Fact>]
let ``matches finds the home page`` () =
    let route: Route<NoBody, string> = Route.get "/"
    Assert.Equal(Some [], Route.matches route [])

[<Fact>]
let ``matches reads each value in order`` () =
    let route: Route<NoBody, string> = Route.get "/a/{first}/b/{second}"
    Assert.Equal(Some [ "1"; "2" ], Route.matches route [ "a"; "1"; "b"; "2" ])

[<Fact>]
let ``matches refuses a different fixed part`` () =
    let route: Route<NoBody, string> = Route.get "/my/{id}/edit"
    Assert.Equal(None, Route.matches route [ "my"; "7"; "delete" ])

[<Fact>]
let ``matches refuses the wrong length`` () =
    let route: Route<NoBody, string> = Route.get "/my/{id}"
    Assert.Equal(None, Route.matches route [ "my" ])

[<Fact>]
let ``at fills the same address as url`` () =
    let route: Route<NoBody, string> = Route.get "/api/places/{slug}"
    Assert.Equal("/api/places/goa", (Route.at [ "slug", "goa" ] [] route).Url)

[<Theory>]
[<InlineData("/api/landing", true)>]
[<InlineData("/api/places?query=goa", true)>]
[<InlineData("/api/feelings/calm/page", true)>]
[<InlineData("/api/auth/options", true)>]
[<InlineData("/api/auth/sign-in", false)>]
[<InlineData("/api/auth/sign-up", false)>]
[<InlineData("/api/me", false)>]
[<InlineData("/api/my/itineraries", false)>]
[<InlineData("/api/itineraries/abc", false)>]
[<InlineData("/api/brand-new-route", false)>]
let ``only answers known to be public may be kept`` (url: string, expected: bool) =
    Assert.Equal(expected, Routes.isPublicAnswer url)

[<Fact>]
let ``url joins several query values`` () =
    let route: Route<NoBody, string> = Route.get "/api/places"
    Assert.Equal("/api/places?a=1&b=2", Route.url [] [ "a", "1"; "b", "2" ] route)

[<Fact>]
let ``url escapes path values`` () =
    let route: Route<NoBody, string> = Route.get "/api/places/{slug}"
    Assert.Equal("/api/places/a%2Fb", Route.url [ "slug", "a/b" ] [] route)

[<Property>]
let ``any value survives an address and its match`` (NonEmptyString value) =
    let route: Route<NoBody, string> = Route.get "/places/{slug}"
    let address = Route.url [ "slug", value ] [] route

    let segments =
        address.Split '/' |> Array.filter (fun part -> part <> "") |> Array.toList

    Route.matches route segments = Some [ Uri.EscapeDataString value ]
