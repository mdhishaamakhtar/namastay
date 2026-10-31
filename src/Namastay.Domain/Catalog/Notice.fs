namespace Namastay.Domain.Catalog

open System

[<RequireQualifiedAccess>]
type NoticeScope =
    | National
    | State
    | PlaceScope
    | LinkScope
    | FestivalScope

[<RequireQualifiedAccess>]
type NoticeKind =
    | Closure
    | Safety
    | Legal
    | Permit

[<RequireQualifiedAccess>]
type Severity =
    | Info
    | Warn
    | Permit
    | Danger

type Notice = {
    Slug: string
    Scope: NoticeScope
    ScopeRef: string option
    Kind: NoticeKind
    Severity: Severity
    Body: string
    ValidFrom: DateOnly option
    ValidTo: DateOnly option
    Evidence: Evidence
}

type Exclusion = {
    Slug: string
    Name: string
    Reason: string
}
