/// The foot of every page someone browses.
module Namastay.Client.Components.Footer

open Feliz
open Namastay.Shared

let private item (label: string) (href: string) =
    Html.a [ prop.key label; prop.href href; prop.text label ]

let view =
    Html.footer [
        prop.className "site-foot"
        prop.children [
            Menu.brand
            Html.p "A free trip planner for first-time visitors to India."
            Html.nav [
                prop.ariaLabel "Footer"
                prop.children [
                    item "Places" Routes.explorePage.Template
                    item "Food" Routes.foodPage.Template
                    item "Plan a trip" Routes.journey.Template
                    item "Contact" Routes.contactPage.Template
                    item "Privacy" Routes.privacyPage.Template
                    item "Terms" Routes.termsPage.Template
                    item "Photo credits" Routes.creditsPage.Template
                ]
            ]
        ]
    ]

/// Holds the footer to the bottom of the window, however short the page is.
let around (page: ReactElement) =
    Html.div [ prop.className "with-foot"; prop.children [ page; view ] ]
