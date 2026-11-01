module Namastay.Client.Components.States

open Feliz
open Namastay.Client.Api

/// An empty matchbox label, so waiting already looks like Namastay.
let loading (label: string) =
    Html.div [
        prop.className "loading"
        prop.role "status"
        prop.children [
            Html.div [
                prop.className "loading-label"
                prop.children [ Html.div [ prop.className "loading-frame" ] ]
            ]
            Html.p label
        ]
    ]

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
