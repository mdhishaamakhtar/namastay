module Namastay.Client.Pages.Home.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Home.State
open Namastay.Shared

let private landing (model: Model) dispatch (navigate: string -> unit) =
    Html.div [
        prop.className "home"
        prop.children [
            Seo.View {
                Title = "Namastay: plan your India trip by swiping"
                Description =
                    "Swipe through places, food and experiences across India, and get a day-by-day itinerary made for you. Free, and written for first-time visitors."
                Path = Routes.home.Template
                Image = None
                Trail = []
            }
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
