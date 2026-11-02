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
            Scene.view
                "/lost.svg"
                "This road doesn't go anywhere"
                "We can't find that page. It may have moved, or the address may be slightly off. Happens to the best travellers."
                "Back to Namastay"
                Routes.home.Template
        ]
    ]

/// For a task page that has no footer of its own.
let withFooter = Footer.around view
