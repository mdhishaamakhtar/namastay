namespace Namastay.Domain.Catalog

type Catalog = {
    Complete: bool
    Regions: Region list
    Clusters: Cluster list
    Families: ColourFamily list
    Interests: Interest list
    Places: Place list
    Links: TransportLink list
    Festivals: Festival list
    Notices: Notice list
    Exclusions: Exclusion list
    Sources: Source list
    Illustrations: Illustration list
    Photos: Photo list
    Cards: Card list
}

[<RequireQualifiedAccess>]
module Catalog =
    let empty = {
        Complete = false
        Regions = []
        Clusters = []
        Families = []
        Interests = []
        Places = []
        Links = []
        Festivals = []
        Notices = []
        Exclusions = []
        Sources = []
        Illustrations = []
        Photos = []
        Cards = []
    }
