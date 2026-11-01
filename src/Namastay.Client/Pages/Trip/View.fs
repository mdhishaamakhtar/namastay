module Namastay.Client.Pages.Trip.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Trip
open Namastay.Client.Pages.Trip.State
open Namastay.Shared
open Namastay.Shared.Contracts

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

let private shareLabel =
    function
    | Sharing.Idle -> "Copy your trip link"
    | Sharing.Copied -> "Link copied"
    | Sharing.Failed -> "Copy the address from your browser"

let private mixBar (share: MixShare) =
    Html.div [
        prop.key share.Name
        prop.className "mix-row"
        prop.children [
            Html.span share.Name
            Html.div [
                prop.className "mix-track"
                prop.children [
                    Html.div [
                        prop.className "mix-fill"
                        prop.style [ style.width (length.percent share.Share) ]
                    ]
                ]
            ]
        ]
    ]

let private panel (model: Model) (trip: Trip) dispatch =
    Html.aside [
        prop.className "trip-panel"
        prop.children [
            Html.a [
                prop.className "journey-brand"
                prop.href Routes.home.Template
                prop.text "Namastay"
            ]
            Html.h1 trip.Title
            RouteMap.view trip.Stops
            if not trip.Mix.IsEmpty then
                Html.div [
                    prop.className "trip-mix"
                    prop.children [ Html.h2 "Your mix"; yield! trip.Mix |> List.map mixBar ]
                ]
            match model.Source with
            | Source.Shared _ ->
                Html.button [
                    prop.className "button trip-share press"
                    prop.text (shareLabel model.Sharing)
                    prop.onClick (fun _ -> dispatch Share)
                ]

                Html.button [
                    prop.className "button trip-share press"
                    prop.text "Save and change this trip"
                    prop.onClick (fun _ -> dispatch SaveToAccount)
                ]
            | Source.Mine id ->
                Html.a [
                    prop.className "button trip-share press"
                    prop.href (Route.url [ "id", id ] [] Routes.editTripPage)
                    prop.text "Change this trip"
                ]

                Html.a [
                    prop.className "trip-link"
                    prop.href Routes.myTripsPage.Template
                    prop.text "My trips"
                ]
            Html.button [
                prop.className "button trip-share press"
                prop.text "Save as PDF"
                prop.onClick (fun _ -> dispatch Print)
            ]
            match model.Problem with
            | Some text -> Html.p [ prop.role "alert"; prop.text text ]
            | None -> Html.none
        ]
    ]

let private swaps (trip: Trip) =
    match trip.Swaps with
    | [] -> Html.none
    | items ->
        Html.section [
            prop.className "trip-note"
            prop.children [
                Html.h2 "We changed something"
                yield!
                    items
                    |> List.map (fun swap -> Html.p [ prop.key swap.From; prop.text swap.Reason ])
            ]
        ]

let private bestMonths (trip: Trip) =
    match trip.BestMonths with
    | [] -> Html.none
    | months ->
        let names =
            months |> List.map (fun month -> monthNames[month - 1]) |> String.concat ", "

        Html.p [ prop.className "trip-best"; prop.text $"Best months for this trip: {names}." ]

let private leftOut (trip: Trip) =
    match trip.LeftOut with
    | [] -> Html.none
    | names ->
        let loved = String.concat ", " names

        Html.section [
            prop.className "trip-note"
            prop.children [
                Html.h2 "Reasons to come back"
                Html.p $"We couldn't fit in everything you loved: {loved}."
            ]
        ]

let private firstDays (stops: TripStop list) =
    stops
    |> List.scan (fun day stop -> day + stop.Days) 1
    |> List.truncate stops.Length

let private stops (model: Model) (saved: SavedTrip) dispatch =
    let trip = saved.Trip

    Html.ol [
        prop.className "trip-stops"
        prop.children (
            List.zip (firstDays trip.Stops) trip.Stops
            |> List.map (fun (day, stop) -> Stop.view saved.ShareToken trip model.Open dispatch day stop)
        )
    ]

let view (model: Model) dispatch =
    match model.Trip with
    | Remote.Loading -> Html.main [ States.loading "Finding your trip" ]
    | Remote.Failed error -> Html.main [ States.failed error (fun () -> dispatch Load) ]
    | Remote.Loaded saved ->
        Html.div [
            prop.className "trip"
            prop.children [
                panel model saved.Trip dispatch
                Html.div [
                    prop.className "trip-content"
                    prop.children [
                        swaps saved.Trip
                        bestMonths saved.Trip
                        stops model saved dispatch
                        leftOut saved.Trip
                    ]
                ]
            ]
        ]
