namespace Namastay.Shared.Contracts

[<RequireQualifiedAccess>]
type ErrorCode =
    | ValidationFailed
    | Unauthorized
    | Forbidden
    | NotFound
    | Conflict
    | MethodNotAllowed
    | UnsupportedMedia
    | TooLarge
    | RateLimited
    | Unavailable
    | Internal

type FieldError = { Field: string; Message: string }

/// The only shape any failed request returns.
type ApiError = {
    Code: ErrorCode
    Message: string
    Fields: FieldError list
    TraceId: string
}

[<RequireQualifiedAccess>]
module ApiError =
    let create code message = {
        Code = code
        Message = message
        Fields = []
        TraceId = ""
    }

    let notFound message = create ErrorCode.NotFound message

    let validation fields = {
        create ErrorCode.ValidationFailed "Please check the details and try again." with
            Fields = fields
    }

type HealthStatus = { Status: string }
