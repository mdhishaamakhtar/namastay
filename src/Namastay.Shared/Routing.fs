namespace Namastay.Shared

open System

/// A route ties a URL to its request and response types, so both sides agree.
type Route<'Request, 'Response> = { Method: string; Template: string }

type NoBody = unit

[<RequireQualifiedAccess>]
module Route =
    let get template : Route<NoBody, 'Response> = { Method = "GET"; Template = template }

    let post template : Route<'Request, 'Response> = { Method = "POST"; Template = template }

    /// Fills `{name}` holes from `path` and appends `query`, escaping every value.
    let url (path: (string * string) list) (query: (string * string) list) (route: Route<'Request, 'Response>) =
        let filled =
            path
            |> List.fold
                (fun (url: string) (name, value) -> url.Replace("{" + name + "}", Uri.EscapeDataString value))
                route.Template

        match query with
        | [] -> filled
        | pairs ->
            pairs
            |> List.map (fun (name, value) -> name + "=" + Uri.EscapeDataString value)
            |> String.concat "&"
            |> fun text -> filled + "?" + text
