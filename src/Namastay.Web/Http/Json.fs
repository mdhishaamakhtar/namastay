module Namastay.Web.Http.Json

open System.Text.Json
open System.Text.Json.Serialization

/// Matches the client's Thoth.Json settings. Change both sides together.
let options =
    let fsharp =
        JsonFSharpOptions
            .Default()
            .WithUnionUnwrapFieldlessTags()
            .WithSkippableOptionFields(SkippableOptionFields.Never)

    let settings = JsonSerializerOptions(JsonSerializerDefaults.Web)
    settings.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    settings.Converters.Add(JsonFSharpConverter fsharp)
    settings
