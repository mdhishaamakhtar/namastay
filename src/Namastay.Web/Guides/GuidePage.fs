module Namastay.Web.Guides.GuidePage

open System
open System.Globalization
open Giraffe.ViewEngine
open Namastay.Shared
open Namastay.Shared.Contracts

let private hedgeNote =
    "Sources differ on this, or we have only one, so check locally."

let private monthNames = [
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

let private lastChecked (text: string) =
    DateOnly.Parse(text, CultureInfo.InvariantCulture).ToString("d MMMM yyyy", CultureInfo.InvariantCulture)

let private shell (pageTitle: string) (description: string) (colours: string) (body': XmlNode list) =
    html [ _lang "en" ] [
        head [] [
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            title [] [ str pageTitle ]
            meta [ _name "description"; _content description ]
            style [] [ rawText colours; rawText GuideStyles.page ]
        ]
        body [] body'
    ]

let private hedge hedged =
    if hedged then
        [ p [ _class "hedge" ] [ str hedgeNote ] ]
    else
        []

let private cost (place: PlaceGuide) =
    let name level =
        match level with
        | 1 -> "Budget"
        | 2 -> "Mid-range"
        | _ -> "Luxury"

    match place.CostLow, place.CostHigh with
    | Some low, Some high when low = high -> name low
    | Some low, Some high -> $"{name low} to {(name high).ToLowerInvariant()}"
    | _ -> "Not confirmed"

let private travellerName =
    function
    | TravellerType.Solo -> "Solo"
    | TravellerType.Couple -> "Couples"
    | TravellerType.Friends -> "Friends"
    | TravellerType.Family -> "Families"

let private facts (place: PlaceGuide) =
    div [ _class "facts" ] [
        div [] [
            strong [] [ str $"{place.DaysMin} to {place.DaysMax} days" ]
            str "How long to stay"
        ]
        div [] [ strong [] [ str (cost place) ]; str "Rough cost" ]
        div [] [
            strong [] [ str (String.Join(", ", place.Suits |> List.map travellerName)) ]
            str "Who it suits"
        ]
        div [] [ strong [] [ str place.State ]; str place.Region ]
    ]

let private ratingClass =
    function
    | MonthRating.Best -> "best"
    | MonthRating.Ok -> "ok"
    | MonthRating.Avoid -> "avoid"
    | MonthRating.Closed -> "closed"
    | MonthRating.Unknown -> "unknown"

let private months (place: PlaceGuide) = [
    h2 [] [ str "When to go" ]
    ul [ _class "months" ] [
        for month in place.Months do
            li [ _class (ratingClass month.Rating) ] [ str monthNames[month.Month - 1] ]
    ]
    p [] [
        str "Dark months are the best time to visit. Black months are ones to avoid."
    ]
]

let private experiences (place: PlaceGuide) = [
    h2 [] [ str "What to do" ]
    for experience in place.Experiences do
        details [] [
            summary [] [ str experience.Name ]
            p [] [ str experience.Description ]
            yield! hedge experience.Hedged
        ]
]

let private stays (place: PlaceGuide) = [
    h2 [] [ str "Where to stay" ]
    for area in place.StayAreas do
        p [] [
            strong [] [ str area.Name ]
            str (area.Note |> Option.map (fun note -> " " + note) |> Option.defaultValue "")
        ]

        yield! hedge area.Hedged
]

let private notices (place: PlaceGuide) =
    match place.Notices with
    | [] -> []
    | items -> [
        h2 [] [ str "Worth knowing" ]
        for item in items do
            div [ _class "warning"; attr "role" "note" ] [ str item.Body; yield! hedge item.Hedged ]
      ]

let private photos (place: PlaceGuide) = [
    for photo in place.Photos do
        figure [] [
            img [ _src photo.Url; _alt photo.Alt; attr "loading" "lazy" ]
            figcaption [] [ str $"{photo.Credit}, "; a [ _href photo.SourceUrl ] [ str photo.Licence ] ]
        ]
]

let private navigation =
    nav [] [
        a [ _href Routes.home.Template ] [ str "Namastay" ]
        a [ _href Routes.journey.Template ] [ str "Plan a trip" ]
    ]

let render (place: PlaceGuide) =
    shell $"{place.Name} travel guide | Namastay" place.Summary (GuideStyles.colours place.Colours) [
        navigation
        header [] [ h1 [] [ str place.Name ]; p [] [ str place.Summary ] ]
        main [] [
            yield! photos place |> List.truncate 1
            facts place
            yield!
                place.IsThisForYou
                |> Option.toList
                |> List.collect (fun text -> [ h2 [] [ str "Is this for you?" ]; p [] [ str text ] ])
            yield! months place
            yield! experiences place
            yield! stays place
            yield! notices place
            yield! photos place |> List.skip (min 1 place.Photos.Length)
            a [ _class "plan"; _href Routes.journey.Template ] [ str "Plan a trip around this" ]
            footer [] [ p [] [ str $"Last checked {lastChecked place.LastChecked}" ] ]
        ]
    ]
    |> RenderView.AsString.htmlDocument

let notFound () =
    shell
        "Guide not found | Namastay"
        "We don't have a guide for that place yet."
        ":root{--deep:#F2B52B;--mid:#F2B52B;--pale:#FFF8E6;--on-deep:#2B2108}"
        [
            navigation
            main [] [
                h1 [] [ str "We don't have that guide yet" ]
                p [] [ str "It may not be written yet, or the address may have changed." ]
                a [ _class "plan"; _href Routes.home.Template ] [ str "Back to Namastay" ]
            ]
        ]
    |> RenderView.AsString.htmlDocument
