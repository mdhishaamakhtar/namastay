module Namastay.Web.Http.Limits

let tripsPolicy = "trips"
let tripsPerMinute = 30
let maxBodyBytes = 64L * 1024L
let signInPolicy = "sign-in"
let signInsPerMinute = 10
