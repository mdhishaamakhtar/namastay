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

let private days (week: DateTime option list) =
    week |> List.map (fun day -> day |> Option.map _.Day |> Option.defaultValue 0)

[<Fact>]
let ``a month is laid out in weeks that start on Monday`` () =
    let weeks = TripDates.weeks (DateTime(2027, 2, 14))

    Assert.Equal<int list list>(
        [
            [ 1; 2; 3; 4; 5; 6; 7 ]
            [ 8; 9; 10; 11; 12; 13; 14 ]
            [ 15; 16; 17; 18; 19; 20; 21 ]
            [ 22; 23; 24; 25; 26; 27; 28 ]
        ],
        weeks |> List.map days
    )

[<Fact>]
let ``a month that starts midweek is padded at both ends`` () =
    let weeks = TripDates.weeks (DateTime(2027, 4, 1))
    Assert.Equal<int list>([ 0; 0; 0; 1; 2; 3; 4 ], days weeks.Head)
    Assert.Equal<int list>([ 26; 27; 28; 29; 30; 0; 0 ], days (List.last weeks))
    Assert.All(weeks, (fun week -> Assert.Equal(7, week.Length)))

[<Fact>]
let ``a month is named with its year`` () =
    Assert.Equal("February 2027", TripDates.monthTitle (DateTime(2027, 2, 14)))

[<Fact>]
let ``a chosen day is written out in full`` () =
    Assert.Equal("Sat 6 Feb 2027", TripDates.full start)

[<Theory>]
[<InlineData(1, true)>]
[<InlineData(5, true)>]
[<InlineData(6, false)>]
[<InlineData(0, false)>]
let ``a trip covers its first day and the days after it`` offset covered =
    Assert.Equal(covered, TripDates.covers start 5 (start.AddDays(float offset - 1.0)))

[<Fact>]
let ``the calendar opens on the month a trip was planned for, the next time it comes round`` () =
    Assert.Equal(DateTime(2027, 2, 1), TripDates.opening (DateTime(2026, 11, 24)) (Some 2))
    Assert.Equal(DateTime(2026, 12, 1), TripDates.opening (DateTime(2026, 11, 24)) (Some 12))
    Assert.Equal(DateTime(2026, 11, 1), TripDates.opening (DateTime(2026, 11, 24)) (Some 11))
    Assert.Equal(DateTime(2026, 11, 1), TripDates.opening (DateTime(2026, 11, 24)) None)
