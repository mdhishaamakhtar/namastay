module Namastay.Client.Components.Dates

let monthNames = [
    "January"
    "February"
    "March"
    "April"
    "May"
    "June"
    "July"
    "August"
    "September"
    "October"
    "November"
    "December"
]

/// Turns the server's 2026-11-01 into 1 November 2026.
let readable (date: string) =
    match date.Split '-' with
    | [| year; month; day |] -> $"{int day} {monthNames[int month - 1]} {year}"
    | _ -> date
