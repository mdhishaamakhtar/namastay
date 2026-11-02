namespace Namastay.Shared

open System

/// A route ties a URL to its request and response types, so both sides agree.
type Route<'Request, 'Response> = { Method: string; Template: string }

type NoBody = unit

/// A filled-in URL that still knows what its route sends and answers.
type Address<'Request, 'Response> = { Url: string }

[<RequireQualifiedAccess>]
module Route =
    let get template : Route<NoBody, 'Response> = { Method = "GET"; Template = template }

    let post template : Route<'Request, 'Response> = { Method = "POST"; Template = template }

    let put template : Route<'Request, 'Response> = { Method = "PUT"; Template = template }

    let delete template : Route<NoBody, 'Response> = {
        Method = "DELETE"
        Template = template
    }

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

    let at path query (route: Route<'Request, 'Response>) : Address<'Request, 'Response> = {
        Url = url path query route
    }

    let address (route: Route<'Request, 'Response>) = at [] [] route

    /// The values in `{name}` holes when `segments` are this route's address, in order.
    let matches (route: Route<'Request, 'Response>) (segments: string list) : string list option =
        let pattern =
            route.Template.Split '/'
            |> Array.filter (fun part -> part <> "")
            |> Array.toList

        let isHole (part: string) = part.StartsWith "{"

        if pattern.Length <> segments.Length then
            None
        else
            let parts = List.zip pattern segments

            if parts |> List.forall (fun (part, segment) -> isHole part || part = segment) then
                parts |> List.filter (fst >> isHole) |> List.map snd |> Some
            else
                None
