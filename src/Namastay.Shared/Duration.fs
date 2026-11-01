module Namastay.Shared.Duration

let private minutesInHour = 60

/// Whole hours, or a half, never a long decimal: 350 minutes is 6.
let private hours minutes =
    let halves = (minutes + 15) / 30

    if halves % 2 = 0 then
        string (halves / 2)
    else
        $"{halves / 2}.5"

let private unit text = if text = "1" then "hour" else "hours"

/// A travel time as a visitor would say it, rounded to the half hour.
let describe shortest longest =
    let first = hours shortest
    let last = hours longest

    if longest < minutesInHour && shortest = longest then
        $"about {shortest} minutes"
    elif longest < minutesInHour then
        $"about {shortest} to {longest} minutes"
    elif shortest < minutesInHour then
        $"about {shortest} minutes to {last} {unit last}"
    elif first = last then
        $"about {first} {unit first}"
    else
        $"about {first} to {last} hours"
