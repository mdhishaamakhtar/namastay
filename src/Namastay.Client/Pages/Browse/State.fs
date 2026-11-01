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
}

type Msg =
    | Load
    | Loaded of Result<Content, Client.ClientError>
    | QueryChanged of string

let private bySlug (slug: string) route = Route.url [ "slug", slug ] [] route

let private held (load: Fable.Core.JS.Promise<Result<'Page, Client.ClientError>>) (wrap: 'Page -> Content) =
    promise {
        let! result = Client.held load
        return result |> Result.map wrap
    }

let private loader target =
    match target with
    | Browse.Places -> held (Client.get<DirectoryRegion list> Routes.directory.Template) Content.Places
    | Browse.Food -> held (Client.get<FoodPage> Routes.food.Template) Content.Food
    | Browse.Region slug -> held (Client.get<BrowsePage>(bySlug slug Routes.regionDetail)) Content.Listed
    | Browse.FoodRegion slug -> held (Client.get<BrowsePage>(bySlug slug Routes.foodRegionDetail)) Content.Listed
    | Browse.Feeling slug -> held (Client.get<BrowsePage>(bySlug slug Routes.feelingDetail)) Content.Listed
    | Browse.Dish slug -> held (Client.get<DishPage>(bySlug slug Routes.dishDetail)) Content.Dish

let private load target =
    Cmd.OfPromise.perform loader target Loaded

let init target =
    {
        Target = target
        Content = Remote.Loading
        Query = ""
    },
    load target

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
    | QueryChanged query -> { model with Query = query }, Cmd.none
