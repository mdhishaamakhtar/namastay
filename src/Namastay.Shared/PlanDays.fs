/// Runs of identical days, so a long stay reads as one line and not seven.
module Namastay.Shared.PlanDays

open Namastay.Shared.Contracts

let group (days: TripDay list) =
    days
    |> List.fold
        (fun groups day ->
            match groups with
            | (first, _, plan) :: rest when plan = day.Plan -> (first, day.Day, plan) :: rest
            | _ -> (day.Day, day.Day, day.Plan) :: groups)
        []
    |> List.rev
