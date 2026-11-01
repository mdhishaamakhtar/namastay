module Namastay.IntegrationTests.Web.SettingsTests

open Microsoft.Extensions.Configuration
open Xunit
open Namastay.Web

let private configuration (pairs: (string * string) list) =
    ConfigurationBuilder()
        .AddInMemoryCollection(
            pairs
            |> List.map (fun (key, value) -> System.Collections.Generic.KeyValuePair(key, value))
        )
        .Build()

let private complete = [
    "ConnectionStrings:Namastay", "Host=db"
    "Media:Endpoint", "http://media"
    "Media:Bucket", "bucket"
    "Media:AccessKey", "key"
    "Media:SecretKey", "secret"
]

[<Fact>]
let ``complete settings load`` () =
    match Settings.load (configuration complete) with
    | Ok settings ->
        Assert.Equal("Host=db", settings.Database)
        Assert.Equal("bucket", settings.Media.Bucket)
    | Error missing -> raise (Xunit.Sdk.XunitException $"unexpected: {missing}")

[<Fact>]
let ``every missing setting is named`` () =
    match Settings.load (configuration [ "Media:Bucket", "bucket" ]) with
    | Ok _ -> raise (Xunit.Sdk.XunitException "expected missing settings")
    | Error missing ->
        Assert.Contains("ConnectionStrings:Namastay", missing)
        Assert.Contains("Media:Endpoint", missing)
        Assert.DoesNotContain("Media:Bucket", missing)

[<Fact>]
let ``blank setting counts as missing`` () =
    let blank =
        complete
        |> List.map (fun (key, value) -> key, (if key = "Media:SecretKey" then " " else value))

    match Settings.load (configuration blank) with
    | Ok _ -> raise (Xunit.Sdk.XunitException "expected a blank setting to fail")
    | Error missing -> Assert.Equal<string list>([ "Media:SecretKey" ], missing)

[<Fact>]
let ``google is off without credentials`` () =
    Assert.True((Settings.google (configuration complete)).IsNone)

[<Fact>]
let ``google needs both settings`` () =
    Assert.True((Settings.google (configuration [ "Google:ClientId", "id" ])).IsNone)

[<Fact>]
let ``google turns on with both settings`` () =
    let settings =
        configuration [ "Google:ClientId", "id"; "Google:ClientSecret", "secret" ]

    Assert.Equal(Some "id", Settings.google settings |> Option.map _.ClientId)
