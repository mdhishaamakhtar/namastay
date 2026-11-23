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
