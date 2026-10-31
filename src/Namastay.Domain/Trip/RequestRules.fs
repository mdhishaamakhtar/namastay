module Namastay.Domain.Trip.RequestRules

open Namastay.Shared.Contracts

let minDays = 5
let maxDays = 21
let private maxAnswers = 1000

/// Returns every problem with a trip request, not just the first.
let check (request: TripRequest) : FieldError list = [
    if request.Days < minDays || request.Days > maxDays then
        {
            Field = "days"
            Message = $"Please choose between {minDays} and {maxDays} days."
        }

    match request.Month with
    | Some month when month < 1 || month > 12 -> {
        Field = "month"
        Message = "Please choose a month from January to December."
      }
    | _ -> ()

    if request.Answers.Length > maxAnswers then
        {
            Field = "answers"
            Message = "That's too many answers."
        }
]
