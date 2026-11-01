module Namastay.Client.Pages.Credits.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Credits.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private row (index: int) (credit: PhotoCredit) =
    Html.li [
        prop.key index
        prop.children [
            Html.strong credit.Place
            Html.span $": {credit.Credit}, "
            Html.a [ prop.href credit.SourceUrl; prop.rel "noreferrer"; prop.text credit.Licence ]
        ]
    ]

let view (model: Model) dispatch =
    Html.div [
        prop.className "browse"
        prop.children [
            Seo.View {
                Title = "Photo credits | Namastay"
                Description = "The photographers whose work appears on Namastay, with each photo's licence."
                Path = Routes.creditsPage.Template
                Image = None
                Trail = [ "Home", "/"; "Photo credits", Routes.creditsPage.Template ]
            }
            Html.header [
                prop.className "browse-head"
                prop.children [
                    Menu.view Menu.Section.Elsewhere
                    Html.div [
                        prop.className "browse-title"
                        prop.children [
                            Html.h1 "Photo credits"
                            Html.p "Thank you to everyone whose photos appear here."
                        ]
                    ]
                ]
            ]
            Html.main [
                prop.children [
                    match model.Credits with
                    | Remote.Loading -> Skeletons.myTrips "Finding the credits"
                    | Remote.Failed error -> States.failed error (fun () -> dispatch Load)
                    | Remote.Loaded credits ->
                        Html.ol [ prop.className "credits"; prop.children (credits |> List.mapi row) ]
                ]
            ]
        ]
    ]
