module Namastay.Client.Pages.Home.View

open Feliz
open Namastay.Client.Pages.Home.State

let private landing (model: Model) dispatch (navigate: string -> unit) =
    Html.div [
        prop.className "home"
        prop.children [
            Hero.view model navigate
            Html.main [
                prop.children [
                    Sections.howItWorks
                    Sections.wall model dispatch
                    Sections.food model
                    Sections.moods "Or start with a feeling" model dispatch
                    Sections.close navigate
                ]
            ]
        ]
    ]

let view (model: Model) dispatch (navigate: string -> unit) =
    match model.Visitor with
    | Visitor.Unknown -> Html.none
    | Visitor.SignedIn trips -> Signed.view model trips dispatch
    | Visitor.SignedOut
    | Visitor.Unsure -> landing model dispatch navigate
