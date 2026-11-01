namespace Namastay.Web

open System
open Microsoft.Extensions.Configuration
open Namastay.Data.Media
open Namastay.Web.Auth.GoogleSignIn

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

    /// Google stays off until both of its settings exist.
    let google (configuration: IConfiguration) =
        let clientId = configuration["Google:ClientId"]
        let clientSecret = configuration["Google:ClientSecret"]

        if String.IsNullOrWhiteSpace clientId || String.IsNullOrWhiteSpace clientSecret then
            None
        else
            Some {
                ClientId = clientId
                ClientSecret = clientSecret
            }
