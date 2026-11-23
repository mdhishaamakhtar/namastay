/// Our own calendar for choosing the day a traveller lands. It shows the whole trip on
/// the month, which a browser's date field can't.
module Namastay.Client.Pages.Trip.Calendar

open System
open Feliz
open Namastay.Client.Components
open Namastay.Shared

type Props = {
    /// Any day in the month on show.
    Shown: DateTime
    Start: DateTime option
    /// How long the trip runs, so its days can be marked.
    Days: int
    Today: DateTime
    OnShow: DateTime -> unit
    OnPick: DateTime -> unit
    /// The arrow keys moved to another day, which may be in another month.
    OnMove: DateTime -> unit
    OnClose: unit -> unit
}

let private weekdays = [ "Mon"; "Tue"; "Wed"; "Thu"; "Fri"; "Sat"; "Sun" ]
let private daysInWeek = 7

/// The button that opens the calendar, which the keyboard goes back to when it closes.
let openerId = "trip-start-button"

/// The element a day's button has, so the keyboard can move to it.
let dayId (date: DateTime) = "day-" + TripDates.format date

let private firstOf (date: DateTime) = DateTime(date.Year, date.Month, 1)

let private step (key: string) =
    match key with
    | "ArrowLeft" -> Some -1
    | "ArrowRight" -> Some 1
    | "ArrowUp" -> Some -daysInWeek
    | "ArrowDown" -> Some daysInWeek
    | _ -> None

let private dayClass (props: Props) (date: DateTime) =
    [
        "calendar-day"
        if props.Start = Some date then
            "calendar-start"
        elif
            props.Start
            |> Option.exists (fun start -> TripDates.covers start props.Days date)
        then
            "calendar-trip"
        if date = props.Today then
            "calendar-today"
    ]
    |> String.concat " "

let private day (props: Props) (date: DateTime) =
    Html.button [
        prop.key (dayId date)
        prop.id (dayId date)
        prop.type'.button
        prop.className (dayClass props date)
        prop.disabled (date < props.Today)
        prop.ariaLabel (TripDates.full date)
        prop.ariaPressed (props.Start = Some date)
        prop.text date.Day
        prop.onClick (fun _ -> props.OnPick date)
        prop.onKeyDown (fun event ->
            match step event.key with
            | Some days ->
                event.preventDefault ()
                props.OnMove(date.AddDays(float days))
            | None -> ())
    ]

let private cell (props: Props) (index: int) (found: DateTime option) =
    match found with
    | Some date -> day props date
    | None -> Html.span [ prop.key $"gap-{index}" ]

let private monthButton (label: string) (flipped: bool) (enabled: bool) (onClick: unit -> unit) =
    Html.button [
        prop.type'.button
        prop.className (
            if flipped then
                "calendar-month calendar-earlier press"
            else
                "calendar-month press"
        )
        prop.ariaLabel label
        prop.disabled (not enabled)
        prop.onClick (fun _ -> onClick ())
        prop.children [ Icons.chevron false ]
    ]

let view (props: Props) =
    let shown = firstOf props.Shown
    let cells = TripDates.weeks shown |> List.concat

    Html.div [
        prop.className "calendar"
        prop.role "group"
        prop.ariaLabel "Choose the day you land"
        // Escape closes the calendar from anywhere inside it.
        prop.onKeyDown (fun event ->
            if event.key = "Escape" then
                props.OnClose())
        prop.children [
            Html.div [
                prop.className "calendar-head"
                prop.children [
                    monthButton "Earlier month" true (shown > firstOf props.Today) (fun () ->
                        props.OnShow(shown.AddMonths -1))
                    Html.strong [ prop.ariaLive.polite; prop.text (TripDates.monthTitle shown) ]
                    monthButton "Later month" false true (fun () -> props.OnShow(shown.AddMonths 1))
                ]
            ]
            Html.div [
                prop.className "calendar-grid"
                prop.children [
                    for name in weekdays ->
                        Html.span [ prop.key name; prop.className "calendar-weekday"; prop.text name ]
                    yield! cells |> List.mapi (cell props)
                ]
            ]
        ]
    ]
