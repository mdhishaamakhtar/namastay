/// What search engines and link previews read about each page.
module Namastay.Client.Components.Seo

open Browser
open Browser.Types
open Fable.Core
open Fable.Core.JsInterop
open Feliz
open Namastay.Client.Api

[<Emit("import.meta.env.VITE_SITE_URL ?? location.origin")>]
let private siteUrl: string = jsNative

let private siteName = "Namastay"
let private shareImage = "/og.png"

type Page = {
    Title: string
    Description: string
    Path: string
    /// An image from the API, shown when the page is shared.
    Image: string option
    /// Names and paths from the home page down to this page.
    Trail: (string * string) list
}

let private upsert (selector: string) (tag: string) (fill: Element -> unit) =
    let found = document.head.querySelector selector

    if isNull found then
        let created = document.createElement tag
        fill created
        document.head.appendChild created |> ignore
    else
        fill found

let private property (name: string) (content: string) =
    upsert $"meta[property=\"{name}\"]" "meta" (fun item ->
        item.setAttribute ("property", name)
        item.setAttribute ("content", content))

let private named (name: string) (content: string) =
    upsert $"meta[name=\"{name}\"]" "meta" (fun item ->
        item.setAttribute ("name", name)
        item.setAttribute ("content", content))

let private crumb (index: int) (name: string, path: string) =
    createObj [
        "@type" ==> "ListItem"
        "position" ==> index + 1
        "name" ==> name
        "item" ==> siteUrl + path
    ]

let private structured (page: Page) =
    let website =
        createObj [
            "@context" ==> "https://schema.org"
            "@type" ==> "WebSite"
            "name" ==> siteName
            "url" ==> siteUrl
        ]

    let breadcrumbs =
        createObj [
            "@context" ==> "https://schema.org"
            "@type" ==> "BreadcrumbList"
            "itemListElement" ==> (page.Trail |> List.mapi crumb |> List.toArray)
        ]

    JS.JSON.stringify (if page.Trail.IsEmpty then website else breadcrumbs)

let private show (page: Page) =
    let address = siteUrl + page.Path

    let image =
        page.Image
        |> Option.map Client.fromApi
        |> Option.defaultValue (siteUrl + shareImage)

    document.title <- page.Title
    named "description" page.Description
    named "robots" "index, follow"
    named "twitter:card" "summary_large_image"
    property "og:site_name" siteName
    property "og:type" "website"
    property "og:title" page.Title
    property "og:description" page.Description
    property "og:url" address
    property "og:image" image

    upsert "link[rel=\"canonical\"]" "link" (fun item ->
        item.setAttribute ("rel", "canonical")
        item.setAttribute ("href", address))

    upsert "script[type=\"application/ld+json\"]" "script" (fun item ->
        item.setAttribute ("type", "application/ld+json")
        item.textContent <- structured page)

let private hide (title: string) =
    document.title <- title
    named "robots" "noindex"

[<ReactComponent>]
let View (page: Page) =
    React.useEffect ((fun () -> show page), [| box page.Path; box page.Title |])
    Html.none

/// For pages that belong to one visitor, which search engines shouldn't list.
[<ReactComponent>]
let Private (title: string) =
    React.useEffect ((fun () -> hide title), [| box title |])
    Html.none
