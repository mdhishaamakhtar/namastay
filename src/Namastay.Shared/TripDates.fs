/// Real dates for a trip's days, once a traveller says when they land.
module Namastay.Shared.TripDates

open System

let private weekdays = [ "Sun"; "Mon"; "Tue"; "Wed"; "Thu"; "Fri"; "Sat" ]

let private months = [
    "Jan"
    "Feb"
    "Mar"
    "Apr"
    "May"
    "Jun"
    "Jul"
    "Aug"
    "Sep"
    "Oct"
    "Nov"
    "Dec"
]

let private weekday (date: DateTime) = weekdays[int date.DayOfWeek]
let private month (date: DateTime) = months[date.Month - 1]

let private whole (date: DateTime) =
    $"{weekday date} {date.Day} {month date}"

/// The dates of a stay that begins on day `firstDay` of a trip starting on `start`:
/// "Sat 6 Feb", "Sun 7 to Tue 9 Feb" or "Sat 27 Feb to Mon 1 Mar".
let range (start: DateTime) (firstDay: int) (days: int) : string =
    let first = start.AddDays(float (firstDay - 1))
    let last = first.AddDays(float (days - 1))

    if days <= 1 then
        whole first
    elif first.Month = last.Month then
        $"{weekday first} {first.Day} to {whole last}"
    else
        $"{whole first} to {whole last}"

let private number (text: string) =
    match Int32.TryParse text with
    | true, value -> Some value
    | _ -> None

/// A year still being typed into a date field is shorter than this.
let private firstFullYear = 1000

/// Reads 2027-02-06, the form a date field and a link carry. A day that doesn't exist is no date.
let parse (text: string) : DateTime option =
    match text.Split '-' |> Array.map number with
    | [| Some year; Some month; Some day |] when
        year >= firstFullYear && year <= 9999 && month >= 1 && month <= 12 && day >= 1
        ->
        if day <= DateTime.DaysInMonth(year, month) then
            Some(DateTime(year, month, day))
        else
            None
    | _ -> None

let private padded (width: int) (value: int) = (string value).PadLeft(width, '0')

/// 2027-02-06, for a date field and a link.
let format (date: DateTime) : string =
    $"{padded 4 date.Year}-{padded 2 date.Month}-{padded 2 date.Day}"

let private monthNames = [
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

let private daysInWeek = 7

/// "February 2027", the heading of a calendar month.
let monthTitle (month: DateTime) : string =
    $"{monthNames[month.Month - 1]} {month.Year}"

/// "Sat 6 Feb 2027", a chosen day with nothing left to guess.
let full (date: DateTime) : string = $"{whole date} {date.Year}"

/// The month a date falls in, as weeks that start on Monday. Days outside the month are
/// None, so every week has seven places.
let weeks (month: DateTime) : DateTime option list list =
    let first = DateTime(month.Year, month.Month, 1)
    // Sunday is 0, so Monday-first puts it last.
    let before = (int first.DayOfWeek + daysInWeek - 1) % daysInWeek
    let length = DateTime.DaysInMonth(month.Year, month.Month)
    let after = (daysInWeek - (before + length) % daysInWeek) % daysInWeek

    List.replicate before None
    @ [ for day in 1..length -> Some(DateTime(month.Year, month.Month, day)) ]
    @ List.replicate after None
    |> List.chunkBySize daysInWeek

/// Whether a date is one of a trip's days, given the day it starts and how long it runs.
let covers (start: DateTime) (days: int) (date: DateTime) : bool =
    date >= start && date < start.AddDays(float days)

/// The month a calendar opens on: the one the trip was planned for, the next time it
/// comes round, or this month when no month was chosen.
let opening (today: DateTime) (plannedMonth: int option) : DateTime =
    let thisMonth = DateTime(today.Year, today.Month, 1)

    match plannedMonth with
    | Some month when month >= today.Month -> DateTime(today.Year, month, 1)
    | Some month -> DateTime(today.Year + 1, month, 1)
    | None -> thisMonth
