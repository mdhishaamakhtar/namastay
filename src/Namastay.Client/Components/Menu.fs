/// The brand and the menu, the same on every page someone browses.
module Namastay.Client.Components.Menu

open Feliz
open Namastay.Shared

[<RequireQualifiedAccess>]
type Section =
    | Home
    | Places
    | Food
    | MyTrips

let brand =
    Html.a [
        prop.className "brand"
        prop.href Routes.home.Template
        prop.children [ Html.img [ prop.src "/logo.svg"; prop.alt "" ]; Html.span "Namastay" ]
    ]

let private item (current: Section) (section: Section) (label: string) (href: string) =
    Html.a [
        prop.key label
        prop.className (
            if section = current then
                "menu-item menu-current"
            else
                "menu-item"
        )
        prop.href href
        prop.text label
    ]

let view (current: Section) =
    Html.div [
        prop.className "menu-bar"
        prop.children [
            brand
            Html.nav [
                prop.className "menu"
                prop.ariaLabel "Main"
                prop.children [
                    item current Section.Home "Home" Routes.home.Template
                    item current Section.Places "Places" Routes.explorePage.Template
                    item current Section.Food "Food" Routes.foodPage.Template
                    item current Section.MyTrips "My trips" Routes.myTripsPage.Template
                ]
            ]
        ]
    ]
