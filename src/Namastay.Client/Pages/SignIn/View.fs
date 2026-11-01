module Namastay.Client.Pages.SignIn.View

open Feliz
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
            prop.href Routes.googleSignIn.Template
            prop.text "Continue with Google"
        ]
    else
        Html.none

let view (model: Model) dispatch =
    let signingUp = model.Mode = Mode.SignUp

    Html.main [
        prop.className "account"
        prop.children [
            Html.a [
                prop.className "account-brand"
                prop.href Routes.home.Template
                prop.text "Namastay"
            ]
            Html.h1 (if signingUp then "Create your account" else "Sign in")
            Html.p "Keep your trips, change them and come back to them."
            google model
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
                        prop.className "button press"
                        prop.type'.submit
                        prop.disabled model.Working
                        prop.text (if signingUp then "Create account" else "Sign in")
                    ]
                ]
            ]
            Html.button [
                prop.className "account-switch"
                prop.type'.button
                prop.text (
                    if signingUp then
                        "I already have an account"
                    else
                        "I'm new here"
                )
                prop.onClick (fun _ -> dispatch SwitchMode)
            ]
        ]
    ]
