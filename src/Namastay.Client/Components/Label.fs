/// The matchbox label: a framed photo over the place's colour.
module Namastay.Client.Components.Label

open Feliz
open Namastay.Client.Api
open Namastay.Shared.Contracts

let view (colour: string) (photo: GuidePhoto option) (name: string) (lines: string list) =
    Html.div [
        prop.className "label"
        prop.children [
            Html.div [
                prop.className "label-frame"
                prop.style [ style.borderColor colour ]
                prop.children [
                    match photo with
                    | Some item ->
                        Html.img [
                            prop.src (Client.fromApi item.Url)
                            prop.alt item.Alt
                            prop.custom ("loading", "lazy")
                        ]
                    | None -> Html.div [ prop.className "label-no-photo"; prop.style [ style.backgroundColor colour ] ]
                    Html.div [
                        prop.className "label-text"
                        prop.style [ style.backgroundColor colour ]
                        prop.children [
                            Html.strong name
                            yield! lines |> List.map Html.span
                            match photo with
                            | Some item -> Html.small $"{item.Credit}, {item.Licence}"
                            | None -> Html.none
                        ]
                    ]
                ]
            ]
        ]
    ]
