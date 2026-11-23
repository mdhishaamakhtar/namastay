namespace Namastay.Domain.Catalog

type Region = {
    Slug: string
    Name: string
    Colour: string option
    Sort: int option
}

type Cluster = {
    Slug: string
    RegionSlug: string
    Name: string
}

/// Where a place is on the map, in degrees north and east.
type Coordinates = { Latitude: float; Longitude: float }

[<RequireQualifiedAccess>]
type Shade =
    | Deep
    | Mid
    | Pale

type ColourFamily = {
    Slug: string
    Name: string
    UsedFor: string
    Deep: string
    Mid: string
    Pale: string
    TextOnDeep: string
}

type Interest = {
    Slug: string
    Name: string
    ParentSlug: string option
    /// Its colour as a feeling on the landing page, chosen so tiles stay easy to tell apart.
    Colour: string option
}
