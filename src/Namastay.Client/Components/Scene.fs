/// A whole-page moment with nothing to list: an illustration, a few words and a way on.
module Namastay.Client.Components.Scene

open Feliz
open Namastay.Shared

let view (art: string) (heading: string) (text: string) (action: string) (href: string) =
    Html.main [
        prop.className "lost"
        prop.children [
            Html.div [
                prop.className "lost-art"
                prop.children [ Html.img [ prop.src art; prop.alt "" ] ]
            ]
            Html.div [
                prop.className "lost-text"
                prop.children [
                    Html.h2 heading
                    Html.p text
                    Html.div [
                        prop.className "lost-actions"
                        prop.children [
                            Html.a [ prop.className "button press"; prop.href href; prop.text action ]
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
