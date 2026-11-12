/// Planning a trip from a feeling page, around that feeling and up to two more.
module Namastay.Client.Pages.Browse.Mix

open Feliz
open Namastay.Client.Components
open Namastay.Client.Pages.Browse.Parts
open Namastay.Client.Pages.Browse.State
open Namastay.Shared
open Namastay.Shared.Contracts

let private mostAdded = TripScopes.maxFeelings - 1
let private bones = 8

let private chip (model: Model) dispatch (feeling: Feeling) =
    let chosen = List.contains feeling.Slug model.Mixed

    Html.button [
        prop.key feeling.Slug
        prop.className "mix-chip press"
        prop.custom ("aria-pressed", chosen)
        // Once the trip is full, only a chosen feeling can still be taken out.
        prop.disabled (not chosen && model.Mixed.Length >= mostAdded)
        prop.onClick (fun _ -> dispatch (MixToggled feeling.Slug))
        prop.text feeling.Name
    ]

let private chips (children: ReactElement list) =
    Html.div [
        prop.className "browse-mix"
        prop.children [
            Html.p "Or mix it with up to two more feelings."
            Html.div [
                prop.className "mix-chips"
                prop.role "group"
                prop.ariaLabel "Feelings to add"
                prop.children children
            ]
        ]
    ]

/// The other feelings, to add to this page's own. Their shapes hold the space while they load.
let private others (page: BrowsePage) (model: Model) dispatch =
    match model.Feelings with
    | Remote.Loading ->
        chips [
            for index in 1..bones -> Html.span [ prop.key (string index); prop.className "mix-chip bone" ]
        ]
    | Remote.Loaded feelings ->
        chips (
            feelings
            |> List.filter (fun feeling -> feeling.Slug <> page.Slug)
            |> List.map (chip model dispatch)
        )
    | Remote.Failed _ -> Html.none

let private label (page: BrowsePage) (added: int) =
    match added with
    | 0 -> $"Plan a {page.Name.ToLowerInvariant()} trip"
    | 1 -> "Plan a trip around both"
    | _ -> "Plan a trip around all three"

/// The feelings added, named as the page lists them. One that isn't listed isn't planned from.
let private added (model: Model) =
    match model.Feelings with
    | Remote.Loaded feelings ->
        model.Mixed
        |> List.choose (fun slug -> feelings |> List.tryFind (fun feeling -> feeling.Slug = slug))
    | _ -> []

/// The plan button for a feeling page, with the feelings it can be mixed with underneath.
let view (page: BrowsePage) (model: Model) dispatch =
    let mixed = added model

    Html.div [
        prop.className "browse-plans"
        prop.children [
            planButton {
                Label = label page mixed.Length
                Scope =
                    Some {
                        Kind = ScopeKind.Feeling
                        Slugs = page.Slug :: (mixed |> List.map _.Slug)
                    }
                Name = Sentence.listed (page.Name :: (mixed |> List.map _.Name))
            }
            others page model dispatch
        ]
    ]
