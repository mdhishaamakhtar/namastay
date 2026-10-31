/// The one place where domain types and API contract types meet.
module Namastay.Domain.Catalog.ContractMapping

open Namastay.Shared.Contracts

let access =
    function
    | Gate.Open -> PlaceAccess.Open
    | Gate.Warn -> PlaceAccess.Warning
    | Gate.Permit -> PlaceAccess.Permit

let severity =
    function
    | Severity.Info -> NoticeSeverity.Info
    | Severity.Warn -> NoticeSeverity.Warning
    | Severity.Permit -> NoticeSeverity.Permit
    | Severity.Danger -> NoticeSeverity.Danger

let travelMode =
    function
    | Mode.Flight -> TravelMode.Flight
    | Mode.Train -> TravelMode.Train
    | Mode.Road -> TravelMode.Road
    | Mode.Ferry -> TravelMode.Ferry
    | Mode.CableCar -> TravelMode.CableCar

let monthRating =
    function
    | Rating.Best -> MonthRating.Best
    | Rating.Ok -> MonthRating.Ok
    | Rating.Avoid -> MonthRating.Avoid
    | Rating.Closed -> MonthRating.Closed
    | Rating.Unknown -> MonthRating.Unknown

let travellerType =
    function
    | Traveller.Solo -> TravellerType.Solo
    | Traveller.Couple -> TravellerType.Couple
    | Traveller.Friends -> TravellerType.Friends
    | Traveller.Family -> TravellerType.Family

let traveller =
    function
    | TravellerType.Solo -> Traveller.Solo
    | TravellerType.Couple -> Traveller.Couple
    | TravellerType.Friends -> Traveller.Friends
    | TravellerType.Family -> Traveller.Family

let budgetLevel =
    function
    | CostLevel.Budget -> BudgetLevel.Budget
    | CostLevel.Mid -> BudgetLevel.Mid
    | CostLevel.Luxury -> BudgetLevel.Luxury

let costLevel =
    function
    | BudgetLevel.Budget -> CostLevel.Budget
    | BudgetLevel.Mid -> CostLevel.Mid
    | BudgetLevel.Luxury -> CostLevel.Luxury
