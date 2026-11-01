module Namastay.Client.Pages.Edit.View

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.Edit.State
open Namastay.Shared
open Namastay.Shared.Contracts

module Search = Namastay.Client.Pages.Home.State

let private longestTrip = 21

let private tripAddress (model: Model) =
    Route.url [ "id", model.Id ] [] Routes.myTripPage

let private control (label: string) (text: string) disabled action =
    Html.button [
        prop.type'.button
        prop.ariaLabel label
        prop.disabled disabled
        prop.text text
        prop.onClick (fun _ -> action ())
    ]

let private dayRange first days =
    if days = 1 then
        $"Day {first}"
    else
        $"Days {first} to {first + days - 1}"

let private ticket dispatch count index first (row: Row) =
    Html.div [
        prop.className "edit-ticket"
        prop.style [ style.backgroundColor row.Deep; style.color row.TextOnDeep ]
        prop.children [
            match row.Photo with
            | Some url -> Html.img [ prop.src url; prop.alt "" ]
            | None -> Html.div [ prop.className "edit-ticket-blank" ]
            Html.div [
                prop.className "edit-ticket-name"
                prop.children [ Html.small (dayRange first row.Days); Html.strong row.Name ]
            ]
            Html.div [
                prop.className "edit-ticket-controls"
                prop.children [
                    Html.div [
                        prop.className "edit-days"
                        prop.children [
                            control $"One day less in {row.Name}" "−" (row.Days <= 1) (fun () ->
                                dispatch (ChangeDays(row.Key, -1)))
                            Html.span (if row.Days = 1 then "1 day" else $"{row.Days} days")
                            control $"One day more in {row.Name}" "+" false (fun () ->
                                dispatch (ChangeDays(row.Key, 1)))
                        ]
                    ]
                    Html.div [
                        prop.className "edit-moves"
                        prop.children [
                            control $"Move {row.Name} up" "↑" (index = 0) (fun () -> dispatch (MoveUp row.Key))
                            control $"Move {row.Name} down" "↓" (index = count - 1) (fun () ->
                                dispatch (MoveDown row.Key))
                            control $"Remove {row.Name}" "×" false (fun () -> dispatch (Remove row.Key))
                        ]
                    ]
                ]
            ]
        ]
    ]

let private results (model: Model) dispatch =
    match model.Search.Results with
    | None -> Html.none
    | Some Remote.Loading -> Html.p "Searching"
    | Some(Remote.Failed error) -> Html.p [ prop.className "form-error"; prop.text (Client.describe error) ]
    | Some(Remote.Loaded []) -> Html.p "We don't have that place. You can add it as your own stop."
    | Some(Remote.Loaded places) ->
        Html.ul [
            prop.className "edit-results"
            prop.children [
                for place in places |> List.filter _.Published ->
                    Html.li [
                        prop.key place.Slug
                        prop.children [
                            Html.button [
                                prop.className "press"
                                prop.type'.button
                                prop.text $"Add {place.Name}"
                                prop.onClick (fun _ -> dispatch (AddPlace place))
                            ]
                        ]
                    ]
            ]
        ]

let private adders (model: Model) dispatch =
    Html.div [
        prop.className "edit-add"
        prop.children [
            Html.label [
                prop.className "form-field"
                prop.children [
                    Html.span "Add a place from our guide"
                    Html.input [
                        prop.type'.search
                        prop.value model.Search.Query
                        prop.onChange (Search.QueryChanged >> SearchMsg >> dispatch)
                    ]
                ]
            ]
            results model dispatch
            Html.label [
                prop.className "form-field"
                prop.children [
                    Html.span "Or somewhere of your own"
                    Html.input [
                        prop.type'.text
                        prop.value model.OwnStop
                        prop.onChange (OwnStopChanged >> dispatch)
                    ]
                ]
            ]
            Html.button [
                prop.className "edit-small-text press"
                prop.type'.button
                prop.text "Add this stop"
                prop.onClick (fun _ -> dispatch AddOwnStop)
            ]
        ]
    ]

let private legText (row: Row) (previous: Row) =
    match row.Arrival with
    | Some leg when row.ComesAfter = Some previous.Key -> Duration.describe leg.MinutesMin leg.MinutesMax
    | _ -> "We'll check this route when you save"

let private mark (row: Row) (previous: Row) =
    match row.Arrival with
    | Some leg when row.ComesAfter = Some previous.Key -> Icons.travel leg.Mode
    | _ -> Icons.travel TravelMode.Road

/// The line between two stops, and the place to add one between them.
let private connector (model: Model) dispatch (previous: Row) (row: Row) =
    Html.div [
        prop.className "edit-leg"
        prop.children [
            Html.div [
                prop.className "stop-leg-line"
                prop.children [
                    Html.span [ prop.className "stop-leg-mark"; prop.children [ mark row previous ] ]
                ]
            ]
            Html.div [
                prop.className "edit-leg-text"
                prop.children [
                    Html.span (legText row previous)
                    Html.button [
                        prop.type'.button
                        prop.text "Add a stop here"
                        prop.onClick (fun _ -> dispatch (AddHere previous.Key))
                    ]
                ]
            ]
        ]
    ]

let private firstDays (rows: Row list) =
    rows |> List.scan (fun day row -> day + row.Days) 1 |> List.truncate rows.Length

let private stops (model: Model) dispatch =
    let rows = model.Rows
    let previous = None :: (rows |> List.map Some)

    Html.div [
        prop.className "edit-stops"
        prop.children [
            for index, (row, first, before) in
                List.zip3 rows (firstDays rows) (List.truncate rows.Length previous)
                |> List.indexed do
                Html.div [
                    prop.key row.Key
                    prop.children [
                        match before with
                        | Some item ->
                            connector model dispatch item row

                            if model.AddAfter = Some item.Key then
                                adders model dispatch
                        | None -> Html.none
                        ticket dispatch rows.Length index first row
                    ]
                ]
        ]
    ]

let private meterPart (row: Row) =
    Html.span [
        prop.key row.Key
        prop.style [ style.custom ("flex", row.Days); style.backgroundColor row.Tint ]
    ]

let private meter (model: Model) =
    let total = model.Rows |> List.sumBy _.Days

    Html.div [
        prop.className "edit-meter"
        prop.children [
            Html.div [
                prop.className "edit-meter-text"
                prop.children [ Html.strong $"{total} days planned"; Html.span "5 to 21 work" ]
            ]
            Html.div [
                prop.className "edit-meter-bar"
                prop.custom ("aria-hidden", "true")
                prop.children [
                    yield! model.Rows |> List.map meterPart
                    if total < longestTrip then
                        Html.span [ prop.key "left"; prop.style [ style.custom ("flex", longestTrip - total) ] ]
                ]
            ]
        ]
    ]

let private monthPicker (model: Model) dispatch =
    Html.label [
        prop.className "edit-month"
        prop.children [
            Html.span "When you're going"
            Html.select [
                prop.value (model.Month |> Option.map string |> Option.defaultValue "")
                prop.onChange (fun (text: string) ->
                    dispatch (MonthChanged(if text = "" then None else Some(int text))))
                prop.children [
                    Html.option [ prop.value ""; prop.text "Not decided" ]
                    yield!
                        Dates.monthNames
                        |> List.mapi (fun index name ->
                            Html.option [ prop.key name; prop.value (index + 1); prop.text name ])
                ]
            ]
        ]
    ]

let private saveButton (model: Model) =
    Html.button [
        prop.className "button press"
        prop.type'.submit
        prop.disabled model.Saving
        prop.text "Save changes"
    ]

let private panel (model: Model) dispatch =
    Html.aside [
        prop.className "trip-panel"
        prop.children [
            Html.a [
                prop.className "journey-brand"
                prop.href (tripAddress model)
                prop.text "Back to your trip"
            ]
            Html.label [
                prop.className "edit-title"
                prop.children [
                    Html.span "Trip name"
                    Html.input [
                        prop.type'.text
                        prop.value model.Title
                        prop.onChange (TitleChanged >> dispatch)
                    ]
                ]
            ]
            meter model
            monthPicker model dispatch
            Html.div [
                prop.className "edit-panel-save"
                prop.children [
                    saveButton model
                    Html.a [
                        prop.className "edit-discard"
                        prop.href (tripAddress model)
                        prop.text "Discard"
                    ]
                ]
            ]
        ]
    ]

let private messages (model: Model) =
    Html.div [
        prop.custom ("aria-live", "polite")
        prop.children [
            for problem in model.Problems do
                Html.p [ prop.key problem; prop.className "form-error"; prop.text problem ]
            for warning in model.Warnings do
                Html.p [ prop.key warning; prop.className "stop-warning"; prop.text warning ]
            if not model.Warnings.IsEmpty then
                Html.a [
                    prop.className "button"
                    prop.href (tripAddress model)
                    prop.text "See your trip"
                ]
        ]
    ]

let private editor (model: Model) dispatch =
    Html.form [
        prop.className "trip reveal"
        prop.onSubmit (fun event ->
            event.preventDefault ()
            dispatch Save)
        prop.children [
            panel model dispatch
            Html.div [
                prop.className "trip-content"
                prop.children [
                    stops model dispatch
                    if model.AddAfter.IsNone then
                        Html.div [ prop.className "edit-end"; prop.children [ adders model dispatch ] ]
                    messages model
                ]
            ]
            Html.div [ prop.className "edit-savebar"; prop.children [ saveButton model ] ]
        ]
    ]

let view (model: Model) dispatch =
    match model.Trip with
    | Remote.Loading -> Skeletons.editor "Finding your trip"
    | Remote.Failed error -> Html.main [ States.failed error (fun () -> dispatch Load) ]
    | Remote.Loaded _ -> editor model dispatch
