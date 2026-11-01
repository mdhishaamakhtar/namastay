/// The plain pages: privacy, terms and contact.
module Namastay.Client.Pages.Reading.View

open Feliz
open Namastay.Client.Components
open Namastay.Client.Routing.Page
open Namastay.Shared

let private contactEmail = "hishaamakhtar2001.mha@gmail.com"

let private privacy = [
    "What we keep",
    [
        "If you make an account: your email address, and your password, stored only as a secure hash we can't read. If you use Google, we get your email address from Google, nothing else."
        "Your saved trips, and the choices you made while planning them."
        "Trips made without an account are kept too, so their share links keep working. They aren't linked to you."
    ]
    "What we don't do", [ "We don't sell your data, show ads or use tracking cookies." ]
    "Cookies",
    [
        "We use one cookie, to keep you signed in. Your browser also remembers that you're signed in, so the right page shows first."
    ]
    "Who else handles it",
    [
        "Railway hosts the site and its database. Google handles sign-in if you choose it."
    ]
    "Your choices",
    [
        "Email us to see your data, correct it or delete your account. We'll reply within 30 days."
    ]
]

let private terms = [
    "Using Namastay",
    [
        "Namastay is free. It's a planning tool, and your trip is yours to check and enjoy."
    ]
    "Check before you travel",
    [
        "We try hard to be accurate, but prices, opening times, permits and rules change, so please confirm anything important before you rely on it. Namastay isn't a travel agent and doesn't book anything."
    ]
    "Your safety",
    [
        "Travel advice can change quickly. Check your government's advice, and take out travel insurance."
    ]
    "Your account", [ "Keep your password safe. Please don't misuse the site, or try to break it." ]
    "Photos and code",
    [
        "Photos belong to their creators and are credited on the site. The code is open source under the GPL-3.0 license, and you'll find it on GitHub."
    ]
]

let private sayHello =
    "Say hello",
    [
        "Found a mistake in a guide? Something closed, moved or wrong? Tell us and we'll fix it. Questions and ideas are welcome too."
    ]

let private photoRemoval =
    "Photo credit or removal",
    [
        "If one of your photos is here and you'd like it credited differently or removed, email us with the page address."
    ]

let private section (heading: string, paragraphs: string list) = [
    Html.h2 heading
    for (text: string) in paragraphs do
        Html.p text
]

let private page (seo: Seo.Page) (title: string) (line: string) (body: ReactElement list) =
    Html.div [
        prop.className "browse"
        prop.children [
            Seo.View seo
            Html.header [
                prop.className "browse-head"
                prop.children [
                    Menu.view Menu.Section.Elsewhere
                    Html.div [ prop.className "browse-title"; prop.children [ Html.h1 title; Html.p line ] ]
                ]
            ]
            Html.main [ prop.className "reading"; prop.children body ]
        ]
    ]

let private seo (name: string) (description: string) (path: string) : Seo.Page = {
    Title = $"{name} | Namastay"
    Description = description
    Path = path
    Image = None
    Trail = [ "Home", "/"; name, path ]
}

let view (reading: ReadingPage) =
    match reading with
    | ReadingPage.Privacy ->
        page
            (seo
                "Privacy"
                "What Namastay keeps about you, and what it doesn't, in plain English."
                Routes.privacyPage.Template)
            "Privacy"
            "Plain English, last updated November 2026."
            (privacy |> List.collect section)
    | ReadingPage.Terms ->
        page
            (seo "Terms" "How Namastay works, and what to check before you travel." Routes.termsPage.Template)
            "Terms"
            "The short version of how this works."
            (terms |> List.collect section)
    | ReadingPage.Contact ->
        page
            (seo "Contact" "Report a mistake in a guide, or say hello." Routes.contactPage.Template)
            "Contact"
            "We read everything."
            [
                yield! section sayHello
                Html.h2 "Email"
                Html.p [
                    prop.children [ Html.a [ prop.href $"mailto:{contactEmail}"; prop.text contactEmail ] ]
                ]
                yield! section photoRemoval
            ]
