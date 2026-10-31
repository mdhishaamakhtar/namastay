namespace Namastay.Domain.Catalog

open System

[<RequireQualifiedAccess>]
type Mode =
    | Flight
    | Train
    | Road
    | Ferry
    | CableCar

type Train = {
    Number: string
    Departs: TimeOnly option
    Arrives: TimeOnly option
    RunDays: string option
    SourceNote: string option
    Evidence: Evidence
}

type TransportLink = {
    Slug: string
    FromSlug: string
    ToSlug: string
    Mode: Mode
    MinutesMin: int
    MinutesMax: int
    DistanceKm: int option
    RunDays: string option
    ValidFrom: DateOnly option
    ValidTo: DateOnly option
    ComfortNote: string option
    Trains: Train list
    Evidence: Evidence
}
