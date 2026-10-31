namespace Namastay.Web

open System
open Microsoft.Extensions.Configuration
open Namastay.Data.Media

type Settings = {
    Database: string
    Media: MediaSettings
}

[<RequireQualifiedAccess>]
module Settings =
    let private required = [
        "ConnectionStrings:Namastay"
        "Media:Endpoint"
        "Media:Bucket"
        "Media:AccessKey"
        "Media:SecretKey"
    ]

    /// Names every missing setting, so a bad deployment fails at startup, not on a request.
    let load (configuration: IConfiguration) : Result<Settings, string list> =
        match
            required
            |> List.filter (fun key -> String.IsNullOrWhiteSpace configuration[key])
        with
        | [] ->
            Ok {
                Database = configuration["ConnectionStrings:Namastay"]
                Media = {
                    Endpoint = configuration["Media:Endpoint"]
                    Bucket = configuration["Media:Bucket"]
                    AccessKey = configuration["Media:AccessKey"]
                    SecretKey = configuration["Media:SecretKey"]
                }
            }
        | missing -> Error missing
