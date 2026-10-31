namespace Namastay.Client.Components

open Namastay.Client.Api.Client

/// Every server call is in exactly one of these states, so none can be forgotten.
[<RequireQualifiedAccess>]
type Remote<'Value> =
    | Loading
    | Loaded of 'Value
    | Failed of ClientError
