namespace Namastay.Domain.Catalog

open System

[<RequireQualifiedAccess>]
type FactStatus =
    | Official
    | TwoSources
    | OneSource
    | Conflict
    | Editorial
    | Unverified

type Evidence = {
    Status: FactStatus
    LastChecked: DateOnly
    SourceIds: int list
}

[<RequireQualifiedAccess>]
type SourceKind =
    | Official
    | Primary
    | Secondary
    | Guide

type Source = {
    Id: int
    Url: string
    Title: string
    Publisher: string
    Kind: SourceKind
    AccessedOn: DateOnly
}
