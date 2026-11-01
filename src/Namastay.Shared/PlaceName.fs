module Namastay.Shared.PlaceName

/// A place can be named for two cities, but people go to the first.
let plain (name: string) = name.Split(" with ")[0]

/// A web-safe name, so one dish has one address wherever it's served.
let slug (name: string) =
    name.ToLowerInvariant().ToCharArray()
    |> Array.map (fun letter ->
        if System.Char.IsLetterOrDigit letter then
            string letter
        else
            "-")
    |> String.concat ""
    |> fun text -> text.Split('-', System.StringSplitOptions.RemoveEmptyEntries)
    |> String.concat "-"
