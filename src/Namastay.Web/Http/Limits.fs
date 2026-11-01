module Namastay.Web.Http.Limits

open System

/// One machine runs the whole browser suite against a local server, so limits there are far higher.
let private scale =
    if Environment.GetEnvironmentVariable "ASPNETCORE_ENVIRONMENT" = "Development" then
        100
    else
        1

let tripsPolicy = "trips"
let tripsPerMinute = 30 * scale
let maxBodyBytes = 64L * 1024L
let signInPolicy = "sign-in"
let signInsPerMinute = 10 * scale
