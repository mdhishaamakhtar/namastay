module Namastay.Client.Pages.Trip.Stop

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Trip.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private modeName =
    function
    | TravelMode.Flight -> "Flight"
    | TravelMode.Train -> "Train"
    | TravelMode.Road -> "Road"
    | TravelMode.Ferry -> "Ferry"
    | TravelMode.CableCar -> "Cable car"

let private legText (leg: TravelLeg) =
    let time = Duration.describe leg.MinutesMin leg.MinutesMax

    let note =
        leg.ComfortNote |> Option.map (fun text -> " " + text) |> Option.defaultValue ""

    $"{modeName leg.Mode}, {time}.{note}"

let private dayRange first days =
    if days = 1 then
        $"Day {first}"
    else
        $"Days {first} to {first + days - 1}"

let private photo (stop: TripStop) =
    match stop.Photo with
    | Some item ->
        Html.figure [
            prop.className "stop-photo"
            prop.children [
                Html.img [ prop.src item.Url; prop.alt item.Alt; prop.custom ("loading", "lazy") ]
                Html.figcaption [ prop.text $"{item.Credit}, {item.Licence}" ]
            ]
        ]
    | None -> Html.none

let private notices (trip: Trip) (stop: TripStop) =
    trip.Notices
    |> List.filter (fun notice -> notice.PlaceSlug = Some stop.PlaceSlug)
    |> List.map (fun notice ->
        Html.p [
            prop.key notice.Body
            prop.className "stop-warning"
            prop.custom ("role", "note")
            prop.text notice.Body
        ])

let private guideLink token (stop: TripStop) =
    let url = Route.url [ "slug", stop.PlaceSlug ] [ "trip", token ] Routes.guidePage

    if stop.Custom then
        Html.p "This is your own stop, so we haven't checked it."
    elif stop.Retired then
        Html.p "This place is no longer in our guide."
    else
        // A guide is a server page, so this is a normal link.
        Html.a [ prop.className "stop-guide"; prop.href url; prop.text "Read the full guide" ]

let private details token (trip: Trip) firstDay (stop: TripStop) =
    Html.div [
        prop.className (
            if stop.Photo.IsSome then
                "stop-body stop-body-photo"
            else
                "stop-body"
        )
        prop.children [
            photo stop
            Html.div [
                prop.className "stop-text"
                prop.children [
                    Html.ul [
                        prop.className "stop-days"
                        prop.children [
                            for day in stop.Plans ->
                                Html.li [
                                    prop.key day.Day
                                    prop.children [ Html.strong $"Day {firstDay + day.Day - 1}"; Html.span day.Plan ]
                                ]
                        ]
                    ]
                    match stop.StayArea with
                    | Some area -> Html.p [ Html.strong "Stay: "; Html.span area ]
                    | None -> Html.none
                    yield! notices trip stop
                    guideLink token stop
                ]
            ]
        ]
    ]

let view token (trip: Trip) (openSlug: string option) dispatch firstDay (stop: TripStop) =
    let isOpen = openSlug = Some stop.PlaceSlug

    Html.li [
        prop.key stop.PlaceSlug
        prop.className "stop"
        prop.children [
            match stop.Arrival with
            | Some leg -> Html.p [ prop.className "stop-leg"; prop.text (legText leg) ]
            | None -> Html.none
            Html.div [
                prop.className "stop-card"
                prop.style [ style.backgroundColor stop.Colours.Pale ]
                prop.children [
                    Html.button [
                        prop.className "stop-head press"
                        prop.style [ style.backgroundColor stop.Colours.Deep; style.color stop.Colours.TextOnDeep ]
                        prop.custom ("aria-expanded", isOpen)
                        prop.onClick (fun _ -> dispatch (Toggle stop.PlaceSlug))
                        prop.children [
                            Html.span [
                                prop.className "stop-title"
                                prop.children [ Html.small (dayRange firstDay stop.Days); Html.strong stop.Name ]
                            ]
                            Icons.chevron isOpen
                        ]
                    ]
                    // Closed stops stay in the page, so a printed trip has every day.
                    Html.div [
                        prop.className (if isOpen then "stop-open" else "stop-closed")
                        prop.children [ details token trip firstDay stop ]
                    ]
                ]
            ]
        ]
    ]
