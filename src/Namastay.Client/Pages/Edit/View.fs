module Namastay.Client.Pages.Edit.View

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.Edit.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private small (label: string) (text: string) disabled action =
    Html.button [
        prop.className "edit-small press"
        prop.type'.button
        prop.ariaLabel label
        prop.disabled disabled
        prop.text text
        prop.onClick (fun _ -> action ())
    ]

let private stopRow dispatch count index (row: Row) =
    Html.li [
        prop.key row.Key
        prop.className "edit-stop"
        prop.children [
            Html.div [
                prop.className "edit-stop-name"
                prop.children [
                    Html.strong row.Name
                    if row.PlaceSlug.IsNone then
                        Html.small "Your own stop"
                ]
            ]
            Html.div [
                prop.className "edit-days"
                prop.children [
                    small $"One day less in {row.Name}" "−" (row.Days <= 1) (fun () ->
                        dispatch (ChangeDays(row.Key, -1)))
                    Html.span $"{row.Days} d"
                    small $"One day more in {row.Name}" "+" false (fun () -> dispatch (ChangeDays(row.Key, 1)))
                ]
            ]
            Html.div [
                prop.className "edit-moves"
                prop.children [
                    small $"Move {row.Name} up" "↑" (index = 0) (fun () -> dispatch (MoveUp row.Key))
                    small $"Move {row.Name} down" "↓" (index = count - 1) (fun () -> dispatch (MoveDown row.Key))
                    small $"Remove {row.Name}" "×" false (fun () -> dispatch (Remove row.Key))
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
                        prop.onChange (Namastay.Client.Pages.Home.State.QueryChanged >> SearchMsg >> dispatch)
                    ]
                ]
            ]
            results model dispatch
            Html.label [
                prop.className "form-field"
                prop.children [
                    Html.span "Or add your own stop"
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

let private monthPicker (model: Model) dispatch =
    Html.label [
        prop.className "form-field"
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

let private messages (model: Model) =
    let tripUrl = Route.url [ "id", model.Id ] [] Routes.myTripPage

    Html.div [
        prop.custom ("aria-live", "polite")
        prop.children [
            for problem in model.Problems do
                Html.p [ prop.key problem; prop.className "form-error"; prop.text problem ]
            for warning in model.Warnings do
                Html.p [ prop.key warning; prop.className "stop-warning"; prop.text warning ]
            if not model.Warnings.IsEmpty then
                Html.a [ prop.className "button"; prop.href tripUrl; prop.text "See your trip" ]
        ]
    ]

let private editor (model: Model) dispatch =
    let total = model.Rows |> List.sumBy _.Days

    Html.form [
        prop.className "edit-form"
        prop.onSubmit (fun event ->
            event.preventDefault ()
            dispatch Save)
        prop.children [
            Html.label [
                prop.className "form-field"
                prop.children [
                    Html.span "Trip name"
                    Html.input [
                        prop.type'.text
                        prop.value model.Title
                        prop.onChange (TitleChanged >> dispatch)
                    ]
                ]
            ]
            monthPicker model dispatch
            Html.h2 $"Your stops, {total} days"
            Html.ol [
                prop.className "edit-stops"
                prop.children (model.Rows |> List.mapi (stopRow dispatch model.Rows.Length))
            ]
            adders model dispatch
            messages model
            Html.button [
                prop.className "button press"
                prop.type'.submit
                prop.disabled model.Saving
                prop.text "Save changes"
            ]
        ]
    ]

let view (model: Model) dispatch =
    Html.div [
        prop.className "trip"
        prop.children [
            Html.aside [
                prop.className "trip-panel"
                prop.children [
                    Html.a [
                        prop.className "journey-brand"
                        prop.href (Route.url [ "id", model.Id ] [] Routes.myTripPage)
                        prop.text "Back to your trip"
                    ]
                    Html.h1 "Change your trip"
                    Html.p "Move stops, change days, or add somewhere of your own."
                ]
            ]
            Html.main [
                prop.className "account"
                prop.children [
                    match model.Trip with
                    | Remote.Loading -> Skeletons.editor "Finding your trip"
                    | Remote.Failed error -> States.failed error (fun () -> dispatch Load)
                    | Remote.Loaded _ -> editor model dispatch
                ]
            ]
        ]
    ]
