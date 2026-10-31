module Namastay.Client.Pages.Home.State

open Elmish
open Fable.Core
open Namastay.Client.Api
open Namastay.Client.Components
open Namastay.Shared
open Namastay.Shared.Contracts

/// Waits for a pause in typing before searching.
let private searchDelay = 200

type Mood = {
    Name: string
    Examples: string
    Search: string
    Colour: string
}

/// Shortcuts into search, not travel data. Each one only fills the search box.
let moods = [
    {
        Name = "Mountains"
        Examples = "Ladakh, Spiti, Parvati"
        Search = "Himalaya"
        Colour = "#2F5D3A"
    }
    {
        Name = "Beaches"
        Examples = "Goa, Gokarna, Varkala"
        Search = "Goa"
        Colour = "#17427A"
    }
    {
        Name = "Faith"
        Examples = "Varanasi, Bodh Gaya"
        Search = "Varanasi"
        Colour = "#B3470F"
    }
    {
        Name = "Monuments"
        Examples = "Agra, Hampi, Jaipur"
        Search = "Agra"
        Colour = "#0F5C4D"
    }
    {
        Name = "Nightlife"
        Examples = "Mumbai, Goa"
        Search = "Mumbai"
        Colour = "#2A1248"
    }
    {
        Name = "Wildlife"
        Examples = "Ranthambore, Kaziranga"
        Search = "Ranthambore"
        Colour = "#4C4A1E"
    }
]

type Model = {
    Query: string
    /// Counts searches, so a slow old answer can't replace a newer one.
    Attempt: int
    Results: Remote<PlaceSummary list> option
}

type Msg =
    | QueryChanged of string
    | PauseEnded of attempt: int
    | Answered of attempt: int * Result<PlaceSummary list, Client.ClientError>
    | Retry

let init () =
    {
        Query = ""
        Attempt = 0
        Results = None
    },
    Cmd.none

let private waitForPause attempt =
    Cmd.OfPromise.perform (fun () -> Promise.sleep searchDelay) () (fun () -> PauseEnded attempt)

let private search (model: Model) =
    let url = Route.url [] [ "query", model.Query.Trim() ] Routes.searchPlaces

    {
        model with
            Results = Some Remote.Loading
    },
    Cmd.OfPromise.perform (fun target -> Client.get<PlaceSummary list> target) url (fun result ->
        Answered(model.Attempt, result))

let update msg (model: Model) =
    match msg with
    | QueryChanged query when query.Trim() = "" ->
        {
            Query = query
            Attempt = model.Attempt + 1
            Results = None
        },
        Cmd.none
    | QueryChanged query ->
        let attempt = model.Attempt + 1

        {
            model with
                Query = query
                Attempt = attempt
        },
        waitForPause attempt
    | PauseEnded attempt when attempt = model.Attempt -> search model
    | Answered(attempt, Ok places) when attempt = model.Attempt ->
        {
            model with
                Results = Some(Remote.Loaded places)
        },
        Cmd.none
    | Answered(attempt, Error error) when attempt = model.Attempt ->
        {
            model with
                Results = Some(Remote.Failed error)
        },
        Cmd.none
    | Retry -> search model
    | PauseEnded _
    | Answered _ -> model, Cmd.none
