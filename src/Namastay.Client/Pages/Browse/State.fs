module Namastay.Client.Pages.Browse.State

open Elmish
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Routing.Page
open Namastay.Shared
open Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type Content =
    | Places of DirectoryRegion list
    | Food of FoodPage
    | Listed of BrowsePage
    | Dish of DishPage

type Model = {
    Target: Browse
    Content: Remote<Content>
    Query: string
    /// Only the Food page offers this.
    Diet: Diet
    /// Every feeling, for a feeling page to offer the others to plan with.
    Feelings: Remote<Feeling list>
    /// The feelings added to a feeling page's own, in the order they were chosen.
    Mixed: string list
}

type Msg =
    | Load
    | Loaded of Result<Content, Client.ClientError>
    | Refreshed of Result<Content, Client.ClientError>
    | QueryChanged of string
    | DietChanged of Diet
    | FeelingsLoaded of Result<Feeling list, Client.ClientError>
    | MixToggled of slug: string

let private bySlug (slug: string) route = Route.at [ "slug", slug ] [] route

let private held (load: Fable.Core.JS.Promise<Result<'Page, Client.ClientError>>) (wrap: 'Page -> Content) =
    promise {
        let! result = Client.held load
        return result |> Result.map wrap
    }

let private loader target =
    match target with
    | Browse.Places -> held (Client.get (Route.address Routes.directory)) Content.Places
    | Browse.Food -> held (Client.get (Route.address Routes.food)) Content.Food
    | Browse.Region slug -> held (Client.get (bySlug slug Routes.regionDetail)) Content.Listed
    | Browse.FoodRegion slug -> held (Client.get (bySlug slug Routes.foodRegionDetail)) Content.Listed
    | Browse.Feeling slug -> held (Client.get (bySlug slug Routes.feelingDetail)) Content.Listed
    | Browse.Dish slug -> held (Client.get (bySlug slug Routes.dishDetail)) Content.Dish

let private load target =
    Cmd.OfPromise.perform loader target Loaded

let private fromSeed target =
    match target with
    | Browse.Places -> Client.seeded (Route.address Routes.directory) |> Option.map Content.Places
    | Browse.Food -> Client.seeded (Route.address Routes.food) |> Option.map Content.Food
    | Browse.Region slug -> Client.seeded (bySlug slug Routes.regionDetail) |> Option.map Content.Listed
    | Browse.FoodRegion slug -> Client.seeded (bySlug slug Routes.foodRegionDetail) |> Option.map Content.Listed
    | Browse.Feeling slug -> Client.seeded (bySlug slug Routes.feelingDetail) |> Option.map Content.Listed
    | Browse.Dish slug -> Client.seeded (bySlug slug Routes.dishDetail) |> Option.map Content.Dish

/// Only a feeling page mixes feelings, so only it asks for them.
let private loadFeelings target =
    match target with
    | Browse.Feeling _ -> Cmd.OfPromise.perform Client.get (Route.address Routes.feelings) FeelingsLoaded
    | _ -> Cmd.none

/// A saved page shows straight away, then quietly catches up with the API.
let init target =
    let started content = {
        Target = target
        Content = content
        Query = ""
        Diet = Diet.Everything
        Feelings = Remote.Loading
        Mixed = []
    }

    match fromSeed target with
    | Some content ->
        started (Remote.Loaded content),
        Cmd.batch [ Cmd.OfPromise.perform loader target Refreshed; loadFeelings target ]
    | None -> started Remote.Loading, Cmd.batch [ load target; loadFeelings target ]

let update msg (model: Model) =
    match msg with
    | Load -> { model with Content = Remote.Loading }, load model.Target
    | Loaded(Ok content) ->
        {
            model with
                Content = Remote.Loaded content
        },
        Cmd.none
    | Loaded(Error error) ->
        {
            model with
                Content = Remote.Failed error
        },
        Cmd.none
    | Refreshed(Ok content) ->
        {
            model with
                Content = Remote.Loaded content
        },
        Cmd.none
    | Refreshed(Error _) -> model, Cmd.none
    | QueryChanged query -> { model with Query = query }, Cmd.none
    | DietChanged diet -> { model with Diet = diet }, Cmd.none
    | FeelingsLoaded(Ok feelings) ->
        {
            model with
                Feelings = Remote.Loaded feelings
        },
        Cmd.none
    // The page plans from its own feeling without them, so the offer to mix just isn't shown.
    | FeelingsLoaded(Error error) ->
        {
            model with
                Feelings = Remote.Failed error
        },
        Cmd.none
    | MixToggled slug ->
        {
            model with
                Mixed = TripScopes.mix slug model.Mixed
        },
        Cmd.none
