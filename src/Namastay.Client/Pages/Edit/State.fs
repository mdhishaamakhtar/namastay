module Namastay.Client.Pages.Edit.State

open Elmish
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Client.Routing
open Namastay.Shared
open Namastay.Shared.Contracts

module Search = Namastay.Client.Pages.Home.State

let private firstStopDays = 2

type Row = {
    Key: int
    PlaceSlug: string option
    Name: string
    Days: int
}

type Model = {
    Id: string
    Trip: Remote<SavedTrip>
    Title: string
    Month: int option
    Rows: Row list
    NextKey: int
    Search: Search.Model
    OwnStop: string
    Saving: bool
    Problems: string list
    Warnings: string list
}

type Msg =
    | Load
    | Loaded of Result<SavedTrip, Client.ClientError>
    | TitleChanged of string
    | MonthChanged of int option
    | MoveUp of int
    | MoveDown of int
    | ChangeDays of key: int * change: int
    | Remove of int
    | SearchMsg of Search.Msg
    | AddPlace of PlaceSummary
    | OwnStopChanged of string
    | AddOwnStop
    | Save
    | Saved of Result<EditedTrip, Client.ClientError>

let private load id =
    Cmd.OfPromise.perform
        (fun url -> Client.held (Client.get<SavedTrip> url))
        (Route.url [ "id", id ] [] Routes.getMine)
        Loaded

let init id =
    let search, _ = Search.init ()

    {
        Id = id
        Trip = Remote.Loading
        Title = ""
        Month = None
        Rows = []
        NextKey = 0
        Search = search
        OwnStop = ""
        Saving = false
        Problems = []
        Warnings = []
    },
    load id

let private toRow index (stop: TripStop) = {
    Key = index
    PlaceSlug = if stop.Custom then None else Some stop.PlaceSlug
    Name = stop.Name
    Days = stop.Days
}

let private swap first second (rows: Row list) =
    rows
    |> List.mapi (fun index row ->
        if index = first then rows[second]
        elif index = second then rows[first]
        else row)

let private move key offset (rows: Row list) =
    match rows |> List.tryFindIndex (fun row -> row.Key = key) with
    | Some index when index + offset >= 0 && index + offset < rows.Length -> swap index (index + offset) rows
    | _ -> rows

let private toEdit (model: Model) : TripEdit = {
    Title = model.Title
    Month = model.Month
    Stops =
        model.Rows
        |> List.map (fun row -> {
            PlaceSlug = row.PlaceSlug
            CustomName = if row.PlaceSlug.IsNone then Some row.Name else None
            Days = row.Days
        })
}

let private save (model: Model) =
    Cmd.OfPromise.perform
        (fun edit -> Client.put<TripEdit, EditedTrip> (Route.url [ "id", model.Id ] [] Routes.editMine) edit)
        (toEdit model)
        Saved

let private addRow slug name (model: Model) = {
    model with
        NextKey = model.NextKey + 1
        Rows =
            model.Rows
            @ [
                {
                    Key = model.NextKey
                    PlaceSlug = slug
                    Name = name
                    Days = firstStopDays
                }
            ]
}

let private showTrip id =
    Cmd.ofEffect (fun _ -> Browser.navigate (Route.url [ "id", id ] [] Routes.myTripPage))


let update msg (model: Model) =
    match msg with
    | Load -> { model with Trip = Remote.Loading }, load model.Id
    | Loaded(Ok saved) ->
        {
            model with
                Trip = Remote.Loaded saved
                Title = saved.Trip.Title
                Month = saved.Trip.Month
                Rows = saved.Trip.Stops |> List.mapi toRow
                NextKey = saved.Trip.Stops.Length
        },
        Cmd.none
    | Loaded(Error error) when Client.isSignedOut error -> model, Cmd.ofEffect (fun _ -> Browser.signInThenReturn ())
    | Loaded(Error error) ->
        {
            model with
                Trip = Remote.Failed error
        },
        Cmd.none
    | TitleChanged text -> { model with Title = text }, Cmd.none
    | MonthChanged month -> { model with Month = month }, Cmd.none
    | MoveUp key ->
        {
            model with
                Rows = move key -1 model.Rows
        },
        Cmd.none
    | MoveDown key ->
        {
            model with
                Rows = move key 1 model.Rows
        },
        Cmd.none
    | ChangeDays(key, change) ->
        {
            model with
                Rows =
                    model.Rows
                    |> List.map (fun row ->
                        if row.Key = key then
                            {
                                row with
                                    Days = max 1 (row.Days + change)
                            }
                        else
                            row)
        },
        Cmd.none
    | Remove key ->
        {
            model with
                Rows = model.Rows |> List.filter (fun row -> row.Key <> key)
        },
        Cmd.none
    | SearchMsg inner ->
        let search, command = Search.update inner model.Search
        { model with Search = search }, Cmd.map SearchMsg command
    | AddPlace place ->
        let search, _ = Search.init ()
        addRow (Some place.Slug) place.Name { model with Search = search }, Cmd.none
    | OwnStopChanged text -> { model with OwnStop = text }, Cmd.none
    | AddOwnStop when model.OwnStop.Trim() = "" -> model, Cmd.none
    | AddOwnStop -> addRow None (model.OwnStop.Trim()) { model with OwnStop = "" }, Cmd.none
    | Save when model.Saving -> model, Cmd.none
    | Save ->
        {
            model with
                Saving = true
                Problems = []
                Warnings = []
        },
        save model
    | Saved(Ok edited) when edited.Warnings.IsEmpty -> { model with Saving = false }, showTrip model.Id
    | Saved(Ok edited) ->
        {
            model with
                Saving = false
                Warnings = edited.Warnings
        },
        Cmd.none
    | Saved(Error(Client.ClientError.Server error)) when not error.Fields.IsEmpty ->
        {
            model with
                Saving = false
                Problems = error.Fields |> List.map _.Message
        },
        Cmd.none
    | Saved(Error error) ->
        {
            model with
                Saving = false
                Problems = [ Client.describe error ]
        },
        Cmd.none
