module Namastay.Client.Pages.Home.View

open Feliz
open Namastay.Shared

let view (navigate: string -> unit) =
    Html.main [
        Html.h1 "Namastay"
        Html.p "Find the India trip that's made for you."
        Html.button [
            prop.className "button"
            prop.text "Plan my trip"
            prop.onClick (fun _ -> navigate Routes.journey.Template)
        ]
    ]
