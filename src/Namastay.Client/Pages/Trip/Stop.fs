module Namastay.Client.Pages.Trip.Stop

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.Trip.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private legTitle (name: string) (leg: TravelLeg) =
    match leg.Mode with
    | TravelMode.Flight -> $"Fly to {name}"
    | TravelMode.Train -> $"Train to {name}"
    | TravelMode.Road -> $"Drive to {name}"
    | TravelMode.Ferry -> $"Ferry to {name}"
    | TravelMode.CableCar -> $"Cable car to {name}"

let private capitalised (text: string) =
    text.Substring(0, 1).ToUpperInvariant() + text.Substring 1

let private legHeadline (name: string) (leg: TravelLeg) =
    Html.span [
        prop.className "stop-leg-text"
        prop.children [
            Html.strong (legTitle name leg)
            Html.span (capitalised (Duration.describe leg.MinutesMin leg.MinutesMax))
        ]
    ]

/// The line that joins two stops. The useful detail opens from it.
let private travelLeg (name: string) (leg: TravelLeg) =
    Html.div [
        prop.className "stop-leg"
        prop.children [
            Html.div [
                prop.className "stop-leg-line"
                prop.children [
                    Html.span [ prop.className "stop-leg-mark"; prop.children [ Icons.travel leg.Mode ] ]
                ]
            ]
            match leg.ComfortNote with
            | Some note ->
                Html.details [
                    prop.className "stop-leg-more"
                    prop.children [ Html.summary [ legHeadline name leg; Icons.chevron false ]; Html.p note ]
                ]
            | None -> Html.div [ prop.className "stop-leg-plain"; prop.children [ legHeadline name leg ] ]
        ]
    ]

let private airportRow (key: string) (title: string) (detail: string) =
    Html.li [
        prop.key key
        prop.className "stop stop-airport"
        prop.children [
            Html.div [
                prop.className "stop-leg"
                prop.children [
                    Html.div [
                        prop.className "stop-leg-line"
                        prop.children [
                            Html.span [
                                prop.className "stop-leg-mark"
                                prop.children [ Icons.travel TravelMode.Flight ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "stop-leg-plain"
                        prop.children [
                            Html.span [
                                prop.className "stop-leg-text"
                                prop.children [ Html.strong title; Html.span detail ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private via (gateway: TripGateway) =
    gateway.Via
    |> Option.map (fun city -> $" Go via {city}.")
    |> Option.defaultValue ""

/// Where the visitor lands, and how they reach the first stop from there.
let landing (firstStop: TripStop) (gateway: TripGateway) = [
    airportRow "land" $"Land in {gateway.City}" (gateway.Airport + via gateway)
    match gateway.Leg with
    | Some leg ->
        Html.li [
            prop.key "land-leg"
            prop.className "stop stop-airport"
            prop.children [ travelLeg firstStop.Name leg ]
        ]
    | None -> Html.none
]

/// How the visitor gets home at the end.
let homeward (gateway: TripGateway) = [
    match gateway.Leg with
    | Some leg ->
        Html.li [
            prop.key "home-leg"
            prop.className "stop stop-airport"
            prop.children [ travelLeg gateway.City leg ]
        ]
    | None -> Html.none
    airportRow "home" $"Fly home from {gateway.City}" (gateway.Airport + via gateway)
]

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
                Html.img [
                    prop.src (Client.fromApi item.Url)
                    prop.alt item.Alt
                    prop.custom ("loading", "lazy")
                ]
                Html.figcaption [ prop.text $"{item.Credit}, {item.Licence}" ]
            ]
        ]
    | None -> Html.none

let private noticeRow (notice: TripNotice) =
    Html.div [
        prop.key notice.Body
        prop.className "stop-notice"
        prop.custom ("role", "note")
        prop.children [ Html.strong (NoticeOrder.kindName notice.Kind); Html.p notice.Body ]
    ]

/// One calm line. It still says so when something could stop the trip.
let private noticesLabel (notices: TripNotice list) =
    let has severity =
        notices |> List.exists (fun notice -> notice.Severity = severity)

    if has NoticeSeverity.Danger then
        "Read this before you go"
    elif has NoticeSeverity.Permit then
        "You need a permit, and a few things to know"
    elif notices.Length = 1 then
        "One thing to know"
    else
        "A few things to know"

let private notices (trip: Trip) (stop: TripStop) =
    match
        trip.Notices
        |> List.filter (fun notice -> notice.PlaceSlug = Some stop.PlaceSlug && notice.Kind <> GuideNoticeKind.Season)
    with
    | [] -> Html.none
    | found ->
        let ordered, _ = NoticeOrder.split _.Severity found.Length found

        Html.details [
            prop.className "stop-notices"
            prop.children [
                Html.summary [ Html.span (noticesLabel found); Icons.chevron false ]
                yield! ordered |> List.map noticeRow
            ]
        ]

let private guideLink token (stop: TripStop) =
    let url =
        Client.fromApi (Route.url [ "slug", stop.PlaceSlug ] [ "trip", token ] Routes.guidePage)

    if stop.Custom then
        Html.p [
            prop.className "stop-note"
            prop.text "This is your own stop, so we haven't checked it."
        ]
    elif stop.Retired then
        Html.p [
            prop.className "stop-note"
            prop.text "This place is no longer in our guide."
        ]
    else
        // A guide is a server page, so this is a normal link.
        Html.a [ prop.className "stop-guide"; prop.href url; prop.text "Read the full guide" ]

/// Days, the stay and the notes share one label column, so every line starts in the same place.
let private row (key: string) (label: string) (content: ReactElement) =
    Html.div [
        prop.key key
        prop.className "stop-row"
        prop.children [ Html.strong label; content ]
    ]

let private dayRow firstDay (first, last, plan: string) =
    let start = firstDay + first - 1
    let finish = firstDay + last - 1

    let label =
        if first = last then
            $"Day {start}"
        else
            $"Days {start} to {finish}"

    row (string first) label (Html.span plan)

let private dishRow (showWord: bool) (dish: TripDish) =
    Html.div [
        prop.key dish.Name
        prop.children [
            Html.strong [
                prop.className "stop-dish"
                prop.children [
                    DietMark.mark dish.Kind
                    Html.span dish.Name
                    if showWord then
                        Html.small (DishKinds.name dish.Kind)
                ]
            ]
            Html.p dish.Description
        ]
    ]

let private eateries diet (found: TripEatery list) =
    let heading =
        if diet = Diet.Everything then
            "Places to try"
        else
            "All-vegetarian kitchens"

    match found with
    | [] -> Html.none
    | _ ->
        Html.div [
            prop.children [
                Html.strong heading
                yield!
                    found
                    |> List.map (fun place -> Html.p [ prop.key place.Name; prop.text $"{place.Name}, {place.Area}" ])
            ]
        ]

let private dietTip diet (dishes: TripDish list) =
    match diet, dishes with
    | Diet.Vegan, [] ->
        Html.p
            "Few dishes here are vegan as usually made. Ask for dal, rice and vegetables with no ghee, butter or curd."
    | Diet.Vegan, _ -> Html.p "Ask for no ghee, butter or curd."
    | Diet.Vegetarian, [] -> Html.p "Ask for dal and vegetable dishes. Most kitchens here will have them."
    | _ -> Html.none

let private eat (trip: Trip) (food: StopFood) =
    let diet = trip.Diet |> Option.defaultValue Diet.Everything

    row
        "eat"
        "Eat"
        (Html.div [
            prop.className "stop-eat"
            prop.children [
                yield! food.Dishes |> List.map (dishRow (diet = Diet.Everything))
                eateries diet food.Eateries
                dietTip diet food.Dishes
            ]
        ])

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
                    yield! stop.Plans |> PlanDays.group |> List.map (dayRow firstDay)
                    match stop.StayArea with
                    | Some area -> row "stay" "Stay" (Html.span area)
                    | None -> Html.none
                    match stop.Food with
                    | Some food -> eat trip food
                    | None -> Html.none
                    notices trip stop
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
            | Some leg -> travelLeg stop.Name leg
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
