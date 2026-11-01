module Namastay.Client.Pages.Trip.RouteMap

open Feliz
open Namastay.Shared.Contracts

/// A stop-by-stop line. It shows the order of the trip, not real geography.
let view (stops: TripStop list) =
    Html.ol [
        prop.className "route"
        prop.custom ("aria-label", "Your route")
        prop.children [
            for stop in stops do
                Html.li [
                    prop.key stop.PlaceSlug
                    prop.children [
                        Html.span [
                            prop.className "route-dot"
                            prop.style [ style.backgroundColor stop.Colours.Deep ]
                        ]
                        Html.span stop.Name
                    ]
                ]
        ]
    ]
