module Namastay.UnitTests.TripScopesTests

open Xunit
open Namastay.Shared
open Namastay.Shared.Contracts

let private feelings slugs = {
    Kind = ScopeKind.Feeling
    Slugs = slugs
}

[<Theory>]
[<InlineData("region", "himalaya")>]
[<InlineData("food", "south-india")>]
[<InlineData("feeling", "beaches")>]
[<InlineData("dish", "dal-baati")>]
let ``one thing of every kind is read from an address`` (kind: string) (slug: string) =
    let scope = TripScopes.ofQuery (Some kind) (Some slug)
    Assert.Equal<string list option>(Some [ slug ], scope |> Option.map _.Slugs)

    Assert.Equal<(string * string) list option>(
        Some [ "scope", kind; "of", slug ],
        scope |> Option.map TripScopes.toQuery
    )

[<Fact>]
let ``several feelings are read in the order they were chosen`` () =
    let scope = TripScopes.ofQuery (Some "feeling") (Some "hindu,beaches,food")
    Assert.Equal(Some(feelings [ "hindu"; "beaches"; "food" ]), scope)

[<Fact>]
let ``an address written from a scope reads back the same`` () =
    let scope = feelings [ "hindu"; "beaches" ]

    let read =
        match TripScopes.toQuery scope with
        | [ (_, kind); (_, named) ] -> TripScopes.ofQuery (Some kind) (Some named)
        | _ -> None

    Assert.Equal(Some scope, read)

[<Theory>]
[<InlineData("feeling", "a,b,c,d")>]
[<InlineData("feeling", "a,a")>]
[<InlineData("feeling", "")>]
[<InlineData("feeling", ",")>]
[<InlineData("region", "north,south")>]
[<InlineData("dish", "one,two")>]
[<InlineData("planet", "mars")>]
let ``an address that names too much, nothing or an unknown kind has no scope`` (kind: string) (named: string) =
    Assert.Equal(None, TripScopes.ofQuery (Some kind) (Some named))

[<Fact>]
let ``an address without both values has no scope`` () =
    Assert.Equal(None, TripScopes.ofQuery None (Some "beaches"))
    Assert.Equal(None, TripScopes.ofQuery (Some "feeling") None)

[<Fact>]
let ``a scope is valid up to three feelings and one of anything else`` () =
    Assert.True(TripScopes.isValid (feelings [ "a"; "b"; "c" ]))
    Assert.False(TripScopes.isValid (feelings [ "a"; "b"; "c"; "d" ]))
    Assert.False(TripScopes.isValid (feelings []))
    Assert.False(TripScopes.isValid (feelings [ "a"; "a" ]))

    Assert.False(
        TripScopes.isValid {
            Kind = ScopeKind.Region
            Slugs = [ "north"; "south" ]
        }
    )

[<Fact>]
let ``mixing adds a feeling and takes it out again`` () =
    Assert.Equal<string list>([ "beaches" ], TripScopes.mix "beaches" [])
    Assert.Equal<string list>([ "beaches"; "food" ], TripScopes.mix "food" [ "beaches" ])
    Assert.Equal<string list>([ "food" ], TripScopes.mix "beaches" [ "beaches"; "food" ])

[<Fact>]
let ``a third feeling can't be mixed in, because the page's own makes three`` () =
    Assert.Equal<string list>([ "beaches"; "food" ], TripScopes.mix "yoga" [ "beaches"; "food" ])

[<Theory>]
[<InlineData("", "")>]
[<InlineData("Beaches", "Beaches")>]
[<InlineData("Beaches|Food", "Beaches and Food")>]
[<InlineData("Beaches|Food|Yoga", "Beaches, Food and Yoga")>]
let ``names are listed the way a sentence lists them`` (names: string) (expected: string) =
    let given =
        names.Split('|', System.StringSplitOptions.RemoveEmptyEntries) |> Array.toList

    Assert.Equal(expected, Sentence.listed given)
