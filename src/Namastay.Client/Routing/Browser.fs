module Namastay.Client.Routing.Browser

open Browser
open Browser.Types

let private changed = "namastay:navigate"

let currentPath () =
    window.location.pathname.Split('/')
    |> Array.filter (fun part -> part <> "")
    |> Array.toList

/// Changes the address without a page load, keeping the browser's back stack.
let navigate (path: string) =
    window.history.pushState (null, "", path)
    window.dispatchEvent (Event.Create changed) |> ignore

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
