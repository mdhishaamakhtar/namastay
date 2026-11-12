module Namastay.UnitTests.Bench.PersonasTests

open Xunit
open Namastay.Bench
open Namastay.Bench.Personas
open Namastay.UnitTests.Trip

let private travellers = Personas.set TripBuilderTests.catalog 7

[<Fact>]
let ``the same seed gives the same travellers`` () =
    Assert.Equal<Persona list>(travellers, Personas.set TripBuilderTests.catalog 7)

[<Fact>]
let ``a different seed gives different travellers`` () =
    Assert.NotEqual<Persona list>(travellers, Personas.set TripBuilderTests.catalog 8)

[<Fact>]
let ``every taste wants an interest a card carries`` () =
    let signalled =
        TripBuilderTests.catalog.Cards
        |> List.collect _.Signals
        |> List.map (fun signal -> signal.Signal.Substring "interest:".Length)

    for persona in travellers |> List.filter (fun persona -> persona.Swiper = Swiper.Taste) do
        Assert.NotEmpty persona.Interests

        for wanted in persona.Interests do
            Assert.Contains(wanted, signalled)

[<Fact>]
let ``random and everything swipers want nothing in particular`` () =
    for persona in travellers |> List.filter (fun persona -> persona.Swiper <> Swiper.Taste) do
        Assert.Empty persona.Interests
        Assert.Empty persona.Feelings

[<Fact>]
let ``every kind of traveller is there`` () =
    let kinds = travellers |> List.map _.Swiper |> List.distinct
    Assert.Equal(3, kinds.Length)

let private coastAndHills = Personas.pairs TripBuilderTests.catalog 7

[<Fact>]
let ``pairs want two different feelings and plan from both`` () =
    Assert.NotEmpty coastAndHills

    for persona in coastAndHills do
        Assert.Equal(2, (List.distinct persona.Interests).Length)
        Assert.Equal<string list>(persona.Interests, persona.Feelings)
        Assert.Equal(Swiper.Taste, persona.Swiper)

[<Fact>]
let ``pairs are the same every run and leave the main set alone`` () =
    Assert.Equal<Persona list>(coastAndHills, Personas.pairs TripBuilderTests.catalog 7)
    Assert.All(travellers, (fun persona -> Assert.True(persona.Feelings.Length <= 1)))
