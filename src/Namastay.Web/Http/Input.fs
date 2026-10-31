module Namastay.Web.Http.Input

open System
open System.Text.RegularExpressions

let private slugPattern = Regex("^[a-z0-9-]{1,80}$", RegexOptions.Compiled)
let private tokenPattern = Regex("^[A-Za-z0-9_-]{12}$", RegexOptions.Compiled)

/// Addresses that can't exist never reach the database.
let isSlug (text: string) = slugPattern.IsMatch text

let isShareToken (text: string) = tokenPattern.IsMatch text

let hasControlCharacters (text: string) = text |> Seq.exists Char.IsControl
