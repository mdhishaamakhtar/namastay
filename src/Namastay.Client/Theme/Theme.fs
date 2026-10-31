module Namastay.Client.Theme.Theme

open Browser
open Fable.Core.JsInterop
open Namastay.Shared.Contracts

let private setColour (name: string) (value: string) =
    document.documentElement?style?setProperty(name, value)

/// Colours come from the API. Nothing about places is coded in the client.
let apply (colours: PlaceColours) =
    setColour "--deep" colours.Deep
    setColour "--mid" colours.Mid
    setColour "--pale" colours.Pale
    setColour "--on-deep" colours.TextOnDeep

/// Back to the brand colours from styles.css.
let reset () =
    for name in [ "--deep"; "--mid"; "--pale"; "--on-deep" ] do
        document.documentElement?style?removeProperty name
