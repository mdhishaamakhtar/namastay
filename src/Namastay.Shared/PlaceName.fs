module Namastay.Shared.PlaceName

/// A place can be named for two cities, but people go to the first.
let plain (name: string) = name.Split(" with ")[0]
