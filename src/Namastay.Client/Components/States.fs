module Namastay.Client.Components.States

open Feliz
open Namastay.Client.Api

let message (text: string) =
    Html.div [ prop.className "state"; prop.text text ]

let failed (error: Client.ClientError) (retry: unit -> unit) =
    Html.div [
        prop.className "state"
        prop.role "alert"
        prop.children [
            Html.p (Client.describe error)
            Html.button [
                prop.className "button"
                prop.text "Try again"
                prop.onClick (fun _ -> retry ())
            ]
        ]
    ]
