module Namastay.Client.Pages.Account.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Account.State
open Namastay.Shared.Contracts

let private row (label: ReactElement) (state: string) =
    Html.div [ prop.className "account-row"; prop.children [ label; Html.strong state ] ]

let private addPassword (model: Model) dispatch =
    Html.form [
        prop.className "my-password"
        prop.onSubmit (fun event ->
            event.preventDefault ()
            dispatch SavePassword)
        prop.children [
            Html.p "Add a password and you can sign in with your email too."
            Html.input [
                prop.className "search-input"
                prop.type' "password"
                prop.ariaLabel "New password"
                prop.placeholder "New password, 10 characters or more"
                prop.custom ("autoComplete", "new-password")
                prop.value model.NewPassword
                prop.onChange (NewPasswordChanged >> dispatch)
            ]
            match model.Problem with
            | Some text -> Html.p [ prop.className "form-error"; prop.role "alert"; prop.text text ]
            | None -> Html.none
            Html.button [ prop.className "button press"; prop.type'.submit; prop.text "Save password" ]
        ]
    ]

let private details (model: Model) (account: Account) dispatch =
    Html.div [
        prop.className "account-details reveal"
        prop.children [
            Html.h2 "Signed in as"
            Html.p [ prop.className "account-email"; prop.text account.Email ]
            Html.h2 "Ways to sign in"
            row
                (Html.span [
                    prop.className "account-way"
                    prop.children [ Icons.google; Html.span "Google" ]
                ])
                (if account.Provider = SignInProvider.Google then
                     "Connected"
                 else
                     "Not connected")
            row (Html.span "Password") (if account.HasPassword then "Set" else "Not set")
            if not account.HasPassword then
                addPassword model dispatch
            if model.Saved then
                Html.p [ prop.className "my-password-saved"; prop.text "Password saved." ]
            Html.button [
                prop.className "account-switch account-out"
                prop.text "Sign out"
                prop.onClick (fun _ -> dispatch SignOut)
            ]
        ]
    ]

let view (model: Model) dispatch =
    Html.div [
        prop.className "browse"
        prop.children [
            Html.header [
                prop.className "browse-head"
                prop.children [
                    Menu.view Menu.Section.Account
                    Html.div [ prop.className "browse-title"; prop.children [ Html.h1 "Your account" ] ]
                ]
            ]
            Html.main [
                prop.className "account-body"
                prop.children [
                    match model.Account with
                    | Remote.Loading -> Skeletons.account "Finding your account"
                    | Remote.Failed error -> States.failed error (fun () -> dispatch Load)
                    | Remote.Loaded account -> details model account dispatch
                ]
            ]
        ]
    ]
