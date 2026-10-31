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
type Severity =
    | Info
    | Warn
    | Permit
    | Danger

type Notice = {
    Slug: string
    Scope: NoticeScope
    ScopeRef: string option
    Kind: string
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
