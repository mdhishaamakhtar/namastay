module Namastay.Client.Pages.SignIn.View

open Feliz
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Pages.SignIn.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private fieldError (model: Model) field =
    model.Fields
    |> List.tryFind (fun item -> item.Field = field)
    |> Option.map (fun item -> Html.p [ prop.className "form-error"; prop.text item.Message ])
    |> Option.defaultValue Html.none

let private input
    (label: string)
    (kind: IReactProperty)
    (autocomplete: string)
    (value: string)
    (changed: string -> unit)
    =
    Html.label [
        prop.className "form-field"
        prop.children [
            Html.span label
            Html.input [
                kind
                prop.custom ("autoComplete", autocomplete)
                prop.required true
                prop.value value
                prop.onChange changed
            ]
        ]
    ]

let private google (model: Model) =
    if model.Google then
        // Google's sign-in is a server page, so this is a normal link.
        Html.a [
            prop.className "button account-google press"
            prop.href (Client.fromApi Routes.googleSignIn.Template)
            prop.text "Continue with Google"
        ]
    else
        Html.none

let private label (model: Model) =
    Html.div [
        prop.className "account-label"
        prop.children [
            Html.div [
                prop.className "account-label-frame"
                prop.children [
                    match model.Photo with
                    | Some photo -> Html.img [ prop.src (Client.fromApi photo.Url); prop.alt photo.Alt ]
                    | None -> Html.div [ prop.className "account-label-blank" ]
                    Html.div [
                        prop.className "account-label-text"
                        prop.children [
                            Html.strong "Your India, kept safe"
                            Html.span "Save a trip once and it's yours to change."
                        ]
                    ]
                ]
            ]
        ]
    ]

let private divider =
    Html.div [
        prop.className "account-divider"
        prop.children [ Html.span "or with your email" ]
    ]

let private switcher (signingUp: bool) dispatch =
    Html.p [
        prop.className "account-switcher"
        prop.children [
            Html.span (
                if signingUp then
                    "Already have an account? "
                else
                    "New here? "
            )
            Html.button [
                prop.className "account-switch"
                prop.type'.button
                prop.text (if signingUp then "Sign in instead" else "Create an account")
                prop.onClick (fun _ -> dispatch SwitchMode)
            ]
        ]
    ]

let private form (model: Model) (signingUp: bool) dispatch =
    Html.form [
        prop.className "account-form"
        prop.custom ("noValidate", true)
        prop.onSubmit (fun event ->
            event.preventDefault ()
            dispatch Submit)
        prop.children [
            input "Email" (prop.type'.email) "email" model.Email (EmailChanged >> dispatch)
            fieldError model "email"
            input
                "Password"
                (prop.type'.password)
                (if signingUp then "new-password" else "current-password")
                model.Password
                (PasswordChanged >> dispatch)
            fieldError model "password"
            match model.Error with
            | Some text -> Html.p [ prop.className "form-error"; prop.role "alert"; prop.text text ]
            | None -> Html.none
            Html.button [
                prop.className "button account-submit press"
                prop.type'.submit
                prop.disabled model.Working
                prop.text (if signingUp then "Create account" else "Sign in")
            ]
        ]
    ]

let view (model: Model) dispatch =
    let signingUp = model.Mode = Mode.SignUp

    Html.div [
        prop.className "account-page"
        prop.children [
            Html.aside [ prop.className "account-stage"; prop.children [ Menu.brand; label model ] ]
            Html.main [
                prop.className "account-main"
                prop.children [
                    Html.div [
                        prop.className "account-column"
                        prop.children [
                            Html.h1 (if signingUp then "Create your account" else "Welcome back")
                            Html.p (
                                if signingUp then
                                    "Keep your trips, change them and come back to them."
                                else
                                    "Sign in to see your trips."
                            )
                            google model
                            if model.Google then
                                divider
                            form model signingUp dispatch
                            switcher signingUp dispatch
                        ]
                    ]
                ]
            ]
        ]
    ]
