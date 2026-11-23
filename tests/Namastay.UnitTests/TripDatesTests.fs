module Namastay.UnitTests.TripDatesTests

open System
open Xunit
open Namastay.Shared

let private start = DateTime(2027, 2, 6)

[<Fact>]
let ``a one-day stay is one date`` () =
    Assert.Equal("Sat 6 Feb", TripDates.range start 1 1)

[<Fact>]
let ``a stay within a month names the month once`` () =
    Assert.Equal("Sun 7 to Tue 9 Feb", TripDates.range start 2 3)

[<Fact>]
let ``a stay across months names both`` () =
    Assert.Equal("Sat 27 Feb to Mon 1 Mar", TripDates.range start 22 3)

[<Fact>]
let ``a date from a link or a date field is read`` () =
    Assert.Equal(Some start, TripDates.parse "2027-02-06")

[<Theory>]
[<InlineData("")>]
[<InlineData("soon")>]
[<InlineData("2027-02-31")>]
[<InlineData("2027-13-01")>]
[<InlineData("2027-02")>]
[<InlineData("0002-02-06")>]
[<InlineData("202-02-06")>]
let ``anything that isn't a real date is no date`` text =
    Assert.Equal(None, TripDates.parse text)

[<Fact>]
let ``a date goes back into a link the way it came out`` () =
    Assert.Equal("2027-02-06", TripDates.format start)
