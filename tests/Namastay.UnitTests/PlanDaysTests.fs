module Namastay.UnitTests.PlanDaysTests

open Xunit
open Namastay.Shared
open Namastay.Shared.Contracts

let private day number plan : TripDay = { Day = number; Plan = plan }

[<Fact>]
let ``days with the same plan in a row become one range`` () =
    let days = [ day 1 "Fort"; day 2 "Free"; day 3 "Free"; day 4 "Free" ]
    Assert.Equal<(int * int * string) list>([ 1, 1, "Fort"; 2, 4, "Free" ], PlanDays.group days)

[<Fact>]
let ``the same plan apart stays apart`` () =
    let days = [ day 1 "Free"; day 2 "Fort"; day 3 "Free" ]
    Assert.Equal(3, (PlanDays.group days).Length)

[<Fact>]
let ``no days means no ranges`` () = Assert.Empty(PlanDays.group [])
