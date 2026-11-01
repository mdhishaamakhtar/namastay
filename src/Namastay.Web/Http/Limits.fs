module Namastay.Web.Http.Limits

open System

/// Rate limits are off on a local server, where one machine sends every request.
/// A limit there would make the browser tests pass or fail by how fast they ran.
let private perMinute limit =
    if Environment.GetEnvironmentVariable "ASPNETCORE_ENVIRONMENT" = "Development" then
        Int32.MaxValue
    else
        limit

let tripsPolicy = "trips"
let tripsPerMinute = perMinute 30
let maxBodyBytes = 64L * 1024L
let signInPolicy = "sign-in"
let signInsPerMinute = perMinute 10
