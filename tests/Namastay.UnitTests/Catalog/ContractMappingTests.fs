module Namastay.UnitTests.Catalog.ContractMappingTests

open Xunit
open Namastay.Domain.Catalog
open Namastay.Shared.Contracts

[<Fact>]
let ``every gate maps to its access`` () =
    Assert.Equal<PlaceAccess list>(
        [ PlaceAccess.Open; PlaceAccess.Warning; PlaceAccess.Permit ],
        [ Gate.Open; Gate.Warn; Gate.Permit ] |> List.map ContractMapping.access
    )

[<Fact>]
let ``every severity maps across`` () =
    Assert.Equal<NoticeSeverity list>(
        [
            NoticeSeverity.Info
            NoticeSeverity.Warning
            NoticeSeverity.Permit
            NoticeSeverity.Danger
        ],
        [ Severity.Info; Severity.Warn; Severity.Permit; Severity.Danger ]
        |> List.map ContractMapping.severity
    )

[<Fact>]
let ``every notice kind maps across`` () =
    Assert.Equal<GuideNoticeKind list>(
        [
            GuideNoticeKind.Closure
            GuideNoticeKind.Safety
            GuideNoticeKind.Legal
            GuideNoticeKind.Permit
        ],
        [ NoticeKind.Closure; NoticeKind.Safety; NoticeKind.Legal; NoticeKind.Permit ]
        |> List.map ContractMapping.noticeKind
    )

[<Fact>]
let ``every month rating maps across`` () =
    Assert.Equal<MonthRating list>(
        [
            MonthRating.Best
            MonthRating.Ok
            MonthRating.Avoid
            MonthRating.Closed
            MonthRating.Unknown
        ],
        [ Rating.Best; Rating.Ok; Rating.Avoid; Rating.Closed; Rating.Unknown ]
        |> List.map ContractMapping.monthRating
    )

[<Fact>]
let ``every travel mode maps across`` () =
    Assert.Equal<TravelMode list>(
        [
            TravelMode.Flight
            TravelMode.Train
            TravelMode.Road
            TravelMode.Ferry
            TravelMode.CableCar
        ],
        [ Mode.Flight; Mode.Train; Mode.Road; Mode.Ferry; Mode.CableCar ]
        |> List.map ContractMapping.travelMode
    )

[<Fact>]
let ``travellers survive a round trip`` () =
    let travellers = [ Traveller.Solo; Traveller.Couple; Traveller.Friends; Traveller.Family ]

    Assert.Equal<Traveller list>(
        travellers,
        travellers
        |> List.map (ContractMapping.travellerType >> ContractMapping.traveller)
    )

[<Fact>]
let ``cost levels survive a round trip`` () =
    let levels = [ CostLevel.Budget; CostLevel.Mid; CostLevel.Luxury ]
    Assert.Equal<CostLevel list>(levels, levels |> List.map (ContractMapping.budgetLevel >> ContractMapping.costLevel))
