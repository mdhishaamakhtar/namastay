module Namastay.UnitTests.NoticeOrderTests

open Xunit
open Namastay.Shared
open Namastay.Shared.Contracts

let private split items = NoticeOrder.split id 3 items

[<Fact>]
let ``the most serious come first`` () =
    let shown, _ =
        split [ NoticeSeverity.Info; NoticeSeverity.Danger; NoticeSeverity.Warning ]

    Assert.Equal<NoticeSeverity list>([ NoticeSeverity.Danger; NoticeSeverity.Warning; NoticeSeverity.Info ], shown)

[<Fact>]
let ``extra notices fold away`` () =
    let shown, rest = split (List.replicate 5 NoticeSeverity.Info)
    Assert.Equal(3, shown.Length)
    Assert.Equal(2, rest.Length)

[<Fact>]
let ``permits and dangers are never folded away`` () =
    let shown, rest =
        split (
            List.replicate 4 NoticeSeverity.Permit
            @ [ NoticeSeverity.Danger; NoticeSeverity.Info ]
        )

    Assert.Equal(5, shown.Length)
    Assert.Equal<NoticeSeverity list>([ NoticeSeverity.Info ], rest)

[<Fact>]
let ``no notices is fine`` () =
    Assert.Equal<NoticeSeverity list * NoticeSeverity list>(([], []), split [])
