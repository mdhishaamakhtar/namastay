module Namastay.UnitTests.RoutingTests

open Xunit
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
let ``url joins several query values`` () =
    let route: Route<NoBody, string> = Route.get "/api/places"
    Assert.Equal("/api/places?a=1&b=2", Route.url [] [ "a", "1"; "b", "2" ] route)

[<Fact>]
let ``url escapes path values`` () =
    let route: Route<NoBody, string> = Route.get "/api/places/{slug}"
    Assert.Equal("/api/places/a%2Fb", Route.url [ "slug", "a/b" ] [] route)
