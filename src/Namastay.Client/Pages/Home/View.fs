module Namastay.Client.Pages.Home.View

open Feliz
open Namastay.Client.Pages.Home.State

let view (model: Model) dispatch (navigate: string -> unit) =
    Html.div [
        prop.className "home"
        prop.children [
            Hero.view model navigate
            Html.main [
                prop.children [
                    Sections.howItWorks
                    Sections.wall model dispatch
                    Sections.food model
                    Sections.moods model dispatch
                    Sections.close navigate
                ]
            ]
        ]
    ]
