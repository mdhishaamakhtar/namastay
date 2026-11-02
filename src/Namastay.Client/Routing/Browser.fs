module Namastay.Client.Routing.Browser

open Browser
open Browser.Types
open Fable.Core
open Fable.Core.JsInterop
open Namastay.Shared

[<Emit("new URLSearchParams($0).get($1)")>]
let private searchParam (search: string) (name: string) : string option = jsNative

let private changed = "namastay:navigate"

let queryParam (name: string) = searchParam window.location.search name

let currentPath () =
    window.location.pathname.Split('/')
    |> Array.filter (fun part -> part <> "")
    |> Array.toList

/// Changes the address for a page that's already showing, so nothing reloads.
let showAddress (path: string) =
    window.history.pushState (null, "", path)

/// Changes the address without a page load, keeping the browser's back stack.
let navigate (path: string) =
    showAddress path
    window.dispatchEvent (Event.Create changed) |> ignore

/// Sends a signed-out visitor to sign in, then back to where they were.
let signInThenReturn () =
    let here = window.location.pathname
    navigate (Route.url [] [ "next", here ] Routes.signInPage)

/// The page to return to after signing in. Only paths on this site count.
let afterSignIn (fallback: string) =
    match searchParam window.location.search "next" with
    | Some path when path.StartsWith "/" && not (path.StartsWith "//") -> path
    | _ -> fallback

/// Calls `onChange` for in-app navigation and for the browser's back and forward buttons.
let subscribe (onChange: string list -> unit) =
    let handler (_: Event) = onChange (currentPath ())
    window.addEventListener ("popstate", handler)
    window.addEventListener (changed, handler)

    {
        new System.IDisposable with
            member _.Dispose() =
                window.removeEventListener ("popstate", handler)
                window.removeEventListener (changed, handler)
    }
