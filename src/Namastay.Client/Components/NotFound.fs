/// The page for an address that goes nowhere, whichever way someone reached it.
module Namastay.Client.Components.NotFound

open Feliz
open Namastay.Shared

let view =
    Html.div [
        prop.className "browse"
        prop.children [
            Html.header [
                prop.className "browse-head"
                prop.children [
                    Menu.view Menu.Section.Elsewhere
                    Html.div [ prop.className "browse-title"; prop.children [ Html.h1 "Lost in India?" ] ]
                ]
            ]
            Html.main [
                prop.className "lost"
                prop.children [
                    Html.div [
                        prop.className "lost-art"
                        prop.children [ Html.img [ prop.src "/lost.svg"; prop.alt "" ] ]
                    ]
                    Html.div [
                        prop.className "lost-text"
                        prop.children [
                            Html.h2 "This road doesn't go anywhere"
                            Html.p
                                "We can't find that page. It may have moved, or the address may be slightly off. Happens to the best travellers."
                            Html.div [
                                prop.className "lost-actions"
                                prop.children [
                                    Html.a [
                                        prop.className "button press"
                                        prop.href Routes.home.Template
                                        prop.text "Back to Namastay"
                                    ]
                                    Html.a [
                                        prop.className "button button-quiet press"
                                        prop.href Routes.explorePage.Template
                                        prop.text "See all places"
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

/// For a task page that has no footer of its own.
let withFooter =
    Html.div [ prop.style [ style.display.contents ]; prop.children [ view; Footer.view ] ]
