module Namastay.UnitTests.DishKindsTests

open Xunit
open Namastay.Shared
open Namastay.Shared.Contracts

[<Fact>]
let ``flags become one kind`` () =
    Assert.Equal(DishKind.NonVeg, DishKinds.ofFlags false false)
    Assert.Equal(DishKind.Vegetarian, DishKinds.ofFlags true false)
    Assert.Equal(DishKind.Vegan, DishKinds.ofFlags true true)

[<Fact>]
let ``each kind has a name for the page`` () =
    Assert.Equal("Non-veg", DishKinds.name DishKind.NonVeg)
    Assert.Equal("Vegetarian", DishKinds.name DishKind.Vegetarian)
    Assert.Equal("Vegan", DishKinds.name DishKind.Vegan)

[<Fact>]
let ``a diet keeps only what it can eat`` () =
    let kept diet =
        [ DishKind.NonVeg; DishKind.Vegetarian; DishKind.Vegan ]
        |> List.filter (DishKinds.suits diet)

    Assert.Equal(3, (kept Diet.Everything).Length)
    Assert.Equal<DishKind list>([ DishKind.Vegetarian; DishKind.Vegan ], kept Diet.Vegetarian)
    Assert.Equal<DishKind list>([ DishKind.Vegan ], kept Diet.Vegan)
