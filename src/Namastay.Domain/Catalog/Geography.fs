namespace Namastay.Domain.Catalog

type Region = { Slug: string; Name: string }

type Cluster = {
    Slug: string
    RegionSlug: string
    Name: string
}

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
}
