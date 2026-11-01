module Namastay.UnitTests.DurationTests

open Xunit
open Namastay.Shared

[<Theory>]
[<InlineData(350, 440, "about 6 to 7.5 hours")>]
[<InlineData(180, 180, "about 3 hours")>]
[<InlineData(175, 185, "about 3 hours")>]
[<InlineData(60, 60, "about 1 hour")>]
[<InlineData(45, 45, "about 45 minutes")>]
[<InlineData(20, 40, "about 20 to 40 minutes")>]
[<InlineData(45, 120, "about 45 minutes to 2 hours")>]
[<InlineData(960, 1200, "about 16 to 20 hours")>]
let ``travel times read simply`` shortest longest (expected: string) =
    Assert.Equal(expected, Duration.describe shortest longest)
