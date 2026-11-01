module Namastay.Web.Guides.GuidePage

open System
open System.Globalization
open Giraffe.ViewEngine
open Namastay.Shared
open Namastay.Shared.Contracts

let private hedgeNote =
    "Sources differ on this, or we have only one, so check locally."

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

let private lastChecked (text: string) =
    DateOnly.Parse(text, CultureInfo.InvariantCulture).ToString("d MMMM yyyy", CultureInfo.InvariantCulture)

let private icon (size: string) (path: XmlNode list) =
    tag
        "svg"
        [
            attr "width" size
            attr "height" size
            attr "viewBox" "0 0 20 20"
            attr "fill" "none"
            attr "stroke" "currentColor"
            attr "stroke-width" "2"
            attr "aria-hidden" "true"
        ]
        path

let private backIcon = icon "20" [ voidTag "path" [ attr "d" "M12.5 4l-6 6l6 6" ] ]
let private openIcon = icon "20" [ voidTag "path" [ attr "d" "M4 7l6 6l6 -6" ] ]

let private shell (pageTitle: string) (description: string) (colours: string) (content: XmlNode list) =
    html [ _lang "en" ] [
        head [] [
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            title [] [ str pageTitle ]
            meta [ _name "description"; _content description ]
            style [] [ rawText colours; rawText GuideStyles.page ]
        ]
        body [] content
    ]

let private hedge hedged =
    if hedged then
        [ p [ _class "hedge" ] [ str hedgeNote ] ]
    else
        []

let private costName =
    function
    | 1 -> "Budget"
    | 2 -> "Mid-range"
    | _ -> "Luxury"

let private cost (place: PlaceGuide) =
    match place.CostLow, place.CostHigh with
    | Some low, Some high when low = high -> costName low
    | Some low, Some high when low = high -> costName low
    | Some low, Some high -> $"{costName low} to {(costName high).ToLowerInvariant()}"
    | _ -> "Not confirmed"

let private travellerName =
    function
    | TravellerType.Solo -> "Solo"
    | TravellerType.Couple -> "Couples"
    | TravellerType.Friends -> "Friends"
    | TravellerType.Family -> "Families"

let private fact (value: string) (label: string) =
    div [] [ strong [] [ str value ]; span [] [ str label ] ]

let private stayLength (place: PlaceGuide) =
    match place.DaysMin, place.DaysMax with
    | 1, 1 -> "1 day"
    | shortest, longest when shortest = longest -> $"{shortest} days"
    | shortest, longest -> $"{shortest} to {longest} days"

let private facts (place: PlaceGuide) =
    div [ _class "facts" ] [
        fact (stayLength place) "How long to stay"
        fact (cost place) "Rough cost"
        fact (String.Join(", ", place.Suits |> List.map travellerName)) "Who it suits"
        fact place.State place.Region
    ]

let private ratingName =
    function
    | MonthRating.Best -> "best"
    | MonthRating.Ok -> "ok"
    | MonthRating.Avoid -> "avoid"
    | MonthRating.Closed -> "closed"
    | MonthRating.Unknown -> "unknown"

let private monthCell (month: GuideMonth) =
    let name = monthNames[month.Month - 1]
    let rating = ratingName month.Rating

    li [ _class rating; attr "aria-label" $"{name}: {rating}" ] [ str (name.Substring(0, 3)) ]

let private months (place: PlaceGuide) =
    section [] [
        h2 [] [ str "When to go" ]
        ul [ _class "months" ] (place.Months |> List.map monthCell)
        p [ _class "note" ] [ str "Go in the coloured months. The black ones are best avoided." ]
    ]

let private intro (place: PlaceGuide) =
    div [ _class "pair" ] [
        yield!
            place.IsThisForYou
            |> Option.toList
            |> List.map (fun text -> section [] [ h2 [] [ str "Is this for you?" ]; p [] [ str text ] ])
        months place
    ]

let private spot (experience: GuideExperience) =
    article [ _class "spot" ] [
        h3 [] [ str experience.Name ]
        p [] [ str experience.Description ]
        yield! hedge experience.Hedged
    ]

let private experiences (place: PlaceGuide) =
    match place.Experiences with
    | [] -> []
    | items -> [
        section [] [ h2 [] [ str "What to do" ]; div [ _class "spots" ] (items |> List.map spot) ]
      ]

let private photoAddress index = $"#photo-{index + 1}"

let private galleryPhoto index (photo: GuidePhoto) =
    figure [] [
        a [ _href (photoAddress index); attr "aria-label" $"See larger: {photo.Alt}" ] [
            img [ _src photo.Url; _alt photo.Alt; attr "loading" "lazy" ]
        ]
        figcaption [] [ str $"{photo.Credit}, "; a [ _href photo.SourceUrl ] [ str photo.Licence ] ]
    ]

let private gallery (place: PlaceGuide) =
    match place.Photos with
    | []
    | [ _ ] -> []
    | _ :: rest -> [
        div [ _class "gallery"; _id "gallery" ] (rest |> List.mapi (fun index photo -> galleryPhoto (index + 1) photo))
      ]

let private stepLink (label: string) (className: string) index =
    a [
        _class $"lightbox-step {className}"
        _href (photoAddress index)
        attr "aria-label" label
    ] []

/// A full-size photo, opened by following its link. Going back closes it, with no script needed.
let private lightbox (count: int) index (photo: GuidePhoto) =
    div [ _class "lightbox"; _id (photoAddress index).[1..] ] [
        a [ _class "lightbox-close"; _href "#gallery"; attr "aria-label" "Close photo" ] []
        figure [] [
            img [ _src photo.Url; _alt photo.Alt; attr "loading" "lazy" ]
            figcaption [] [
                str $"{photo.Alt}. {photo.Credit}, "
                a [ _href photo.SourceUrl ] [ str photo.Licence ]
            ]
        ]
        if count > 1 then
            stepLink "Previous photo" "lightbox-previous" ((index + count - 1) % count)
            stepLink "Next photo" "lightbox-next" ((index + 1) % count)
    ]

let private lightboxes (place: PlaceGuide) =
    place.Photos |> List.mapi (lightbox place.Photos.Length)

let private stayArea (area: GuideStay) = [
    p [] [
        strong [] [ str area.Name ]
        str (area.Note |> Option.map (fun note -> " " + note) |> Option.defaultValue "")
    ]
    yield! hedge area.Hedged
]

let private logistics (place: PlaceGuide) =
    div [ _class "pair" ] [
        yield!
            place.GettingThere
            |> Option.toList
            |> List.map (fun text -> section [] [ h2 [] [ str "Getting there" ]; p [] [ str text ] ])
        section [ _class "stays" ] [
            h2 [] [ str "Where to stay" ]
            yield! place.StayAreas |> List.collect stayArea
        ]
    ]

let private shownNotices = 3

let private noticeRow (item: GuideNotice) =
    div [ _class "notice" ] [
        strong [] [ str (NoticeOrder.kindName item.Kind) ]
        div [] [ p [] [ str item.Body ]; yield! hedge item.Hedged ]
    ]

let private moreNotices (rest: GuideNotice list) =
    match rest with
    | [] -> []
    | items -> [
        details [ _class "more-notices" ] [
            summary [] [ str $"{items.Length} more to know"; openIcon ]
            div [] (items |> List.map noticeRow)
        ]
      ]

let private notices (place: PlaceGuide) =
    match NoticeOrder.split _.Severity shownNotices place.Notices with
    | [], _ -> []
    | shown, rest -> [
        section [ _class "notices"; attr "role" "note" ] [
            h2 [] [ str "Worth knowing" ]
            yield! shown |> List.map noticeRow
            yield! moreNotices rest
        ]
      ]

let private planLink (label: string) =
    a [ _class "plan"; _href Routes.journey.Template ] [ str label ]

let private topBar (trip: string option) =
    div [ _class "top" ] [
        match trip with
        | Some token ->
            a [ _class "back"; _href (Route.url [ "shareToken", token ] [] Routes.trip) ] [ backIcon; str "Your trip" ]
        | None -> a [ _class "back"; _href Routes.journey.Template ] [ str "Plan a trip" ]
        a [ _class "brand"; _href Routes.home.Template ] [ str "Namastay" ]
    ]

let private heroLabel (place: PlaceGuide) =
    match place.Photos with
    | [] -> []
    | photo :: _ -> [
        div [ _class "label" ] [
            div [ _class "frame" ] [
                div [ _class "hero" ] [
                    a [ _href (photoAddress 0); attr "aria-label" $"See larger: {photo.Alt}" ] [
                        img [ _src photo.Url; _alt photo.Alt ]
                    ]
                ]
                p [ _class "credit" ] [ str $"{photo.Credit}, "; a [ _href photo.SourceUrl ] [ str photo.Licence ] ]
            ]
            yield!
                place.MarkUrl
                |> Option.toList
                |> List.map (fun url -> div [ _class "mark" ] [ img [ _src url; _alt "" ] ])
        ]
      ]

let private panel trip (place: PlaceGuide) =
    tag "aside" [ _class "panel" ] [
        topBar trip
        yield! heroLabel place
        h1 [] [ str place.Name ]
        p [ _class "summary" ] [ str place.Summary ]
        planLink "Plan a trip around this"
    ]

let private content (place: PlaceGuide) =
    main [ _class "content" ] [
        facts place
        intro place
        yield! experiences place
        yield! gallery place
        logistics place
        yield! notices place
        planLink "Plan a trip around this"
        footer [] [ str $"Last checked {lastChecked place.LastChecked}" ]
    ]

/// `trip` adds a way back to the itinerary that led here.
let render (trip: string option) (place: PlaceGuide) =
    shell $"{place.Name} travel guide | Namastay" place.Summary (GuideStyles.colours place.Colours) [
        div [ _class "guide" ] [ panel trip place; content place ]
        yield! lightboxes place
    ]
    |> RenderView.AsString.htmlDocument

let notFound () =
    shell
        "Guide not found | Namastay"
        "We don't have a guide for that place yet."
        ":root{--deep:#F2B52B;--mid:#F2B52B;--pale:#FFF8E6;--on-deep:#2B2108}"
        [
            main [ _class "content" ] [
                h1 [] [ str "We don't have that guide yet" ]
                p [] [ str "It may not be written yet, or the address may have changed." ]
                a [ _class "plan"; _href Routes.home.Template ] [ str "Back to Namastay" ]
            ]
        ]
    |> RenderView.AsString.htmlDocument
