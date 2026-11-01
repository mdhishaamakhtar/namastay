module Namastay.UnitTests.Catalog.ValidationTests

open System
open Xunit
open Namastay.Domain.Catalog
open Namastay.Domain.Catalog.Validation

let evidence = {
    Status = FactStatus.TwoSources
    LastChecked = DateOnly(2026, 10, 1)
    SourceIds = [ 1 ]
}

let place: Place = {
    Slug = "kasol"
    ClusterSlug = "kullu"
    Name = "Kasol"
    State = "Himachal Pradesh"
    FamilySlug = "pine"
    Shade = Shade.Deep
    Gate = Gate.Open
    Popularity = Popularity.Between
    DaysMin = 2
    DaysMax = 4
    CostLow = None
    CostHigh = None
    Summary = "A valley town."
    IsThisForYou = None
    GettingThere = None
    Airport = None
    Hub = false
    FoodIntro = None
    Dishes = []
    Eateries = []
    Published = true
    Interests = []
    Months = [
        {
            Month = 5
            Rating = Rating.Best
            Note = None
            Evidence = evidence
        }
    ]
    TravellerFit = []
    Experiences = []
    DayPlans = [
        {
            Variant = "short"
            Day = 1
            Plan = "Walk."
            Evidence = evidence
        }
    ]
    StayAreas = [
        {
            Name = "Old Kasol"
            CostLevel = CostLevel.Budget
            Note = None
            Evidence = evidence
        }
    ]
    Evidence = evidence
}

let link = {
    Slug = "kasol-manali"
    FromSlug = "kasol"
    ToSlug = "manali"
    Mode = Mode.Road
    MinutesMin = 120
    MinutesMax = 180
    DistanceKm = Some 80
    RunDays = None
    ValidFrom = None
    ValidTo = None
    ComfortNote = None
    Trains = []
    Evidence = evidence
}

let catalog = {
    Catalog.empty with
        Regions = [
            {
                Slug = "himalaya"
                Name = "Himalaya"
                Colour = None
                Sort = None
            }
        ]
        Clusters = [
            {
                Slug = "kullu"
                RegionSlug = "himalaya"
                Name = "Kullu"
            }
        ]
        Families = [
            {
                Slug = "pine"
                Name = "Pine"
                UsedFor = "Valleys"
                Deep = "#2F5D3A"
                Mid = "#4C7A52"
                Pale = "#DCE6C8"
                TextOnDeep = "#FFFFFF"
            }
        ]
        Sources = [
            {
                Id = 1
                Url = "https://example.org"
                Title = "T"
                Publisher = "P"
                Kind = SourceKind.Official
                AccessedOn = DateOnly(2026, 10, 1)
            }
        ]
        Places = [
            place
            {
                place with
                    Slug = "manali"
                    Name = "Manali"
            }
        ]
        Links = [ link ]
}

let hasError (text: string) (catalog: Catalog) =
    check catalog |> List.exists (fun error -> error.Message.Contains text)

[<Fact>]
let ``valid catalog passes`` () = Assert.Empty(check catalog)

[<Fact>]
let ``missing reference fails`` () =
    let broken = {
        catalog with
            Links = [ { link with ToSlug = "nowhere" } ]
    }

    Assert.True(hasError "nowhere" broken)

[<Fact>]
let ``month outside 1-12 fails`` () =
    let broken = {
        catalog with
            Places = [
                {
                    place with
                        Months = { place.Months.Head with Month = 13 } :: place.Months
                }
            ]
    }

    Assert.True(hasError "month 13" broken)

[<Fact>]
let ``published place needs best months`` () =
    let broken = {
        catalog with
            Places = [ { place with Months = [] } ]
    }

    Assert.True(hasError "best month" broken)

[<Fact>]
let ``published place needs stay area`` () =
    let broken = {
        catalog with
            Places = [ { place with StayAreas = [] } ]
    }

    Assert.True(hasError "stay area" broken)

[<Fact>]
let ``published place needs day plan`` () =
    let broken = {
        catalog with
            Places = [ { place with DayPlans = [] } ]
    }

    Assert.True(hasError "day plan" broken)

[<Fact>]
let ``unpublished place can be incomplete`` () =
    let draft = {
        place with
            Slug = "draft"
            Published = false
            Months = []
            StayAreas = []
            DayPlans = []
    }

    Assert.Empty(
        check {
            catalog with
                Places = draft :: catalog.Places
        }
    )

[<Fact>]
let ``route needs positive minutes`` () =
    let broken = {
        catalog with
            Links = [ { link with MinutesMin = 0 } ]
    }

    Assert.True(hasError "minutes" broken)

[<Fact>]
let ``valid to must follow valid from`` () =
    let broken = {
        catalog with
            Links = [
                {
                    link with
                        ValidFrom = Some(DateOnly(2026, 5, 1))
                        ValidTo = Some(DateOnly(2026, 4, 1))
                }
            ]
    }

    Assert.True(hasError "valid_to" broken)

[<Fact>]
let ``fact needs source`` () =
    let broken = {
        catalog with
            Links = [
                {
                    link with
                        Evidence = { evidence with SourceIds = [] }
                }
            ]
    }

    Assert.True(hasError "no source" broken)

[<Fact>]
let ``editorial fact needs no source`` () =
    let opinion = {
        catalog with
            Links = [
                {
                    link with
                        Evidence = {
                            evidence with
                                Status = FactStatus.Editorial
                                SourceIds = []
                        }
                }
            ]
    }

    Assert.Empty(check opinion)

[<Fact>]
let ``unverified fact in visitor copy fails`` () =
    let broken = {
        catalog with
            Places = [
                {
                    place with
                        Experiences = [
                            {
                                Slug = "x"
                                Name = "X"
                                Description = "Y"
                                Sort = 0
                                Evidence = {
                                    evidence with
                                        Status = FactStatus.Unverified
                                }
                            }
                        ]
                }
            ]
    }

    Assert.True(hasError "unverified" broken)

[<Fact>]
let ``all errors are returned`` () =
    let broken = {
        catalog with
            Places = [
                {
                    place with
                        Months = []
                        StayAreas = []
                        DayPlans = []
                }
            ]
            Links = []
    }

    Assert.Equal(3, (check broken).Length)

[<Fact>]
let ``unknown source id fails`` () =
    let broken = {
        catalog with
            Links = [
                {
                    link with
                        Evidence = { evidence with SourceIds = [ 99 ] }
                }
            ]
    }

    Assert.True(hasError "unknown source 99" broken)

[<Fact>]
let ``notice for a place needs a known place`` () =
    let notice = {
        Slug = "n"
        Scope = NoticeScope.PlaceScope
        ScopeRef = Some "atlantis"
        Kind = NoticeKind.Permit
        Severity = Severity.Permit
        Body = "Needs a permit."
        ValidFrom = None
        ValidTo = None
        Evidence = evidence
    }

    Assert.True(hasError "atlantis" { catalog with Notices = [ notice ] })

    Assert.True(
        hasError "scope reference" {
            catalog with
                Notices = [ { notice with ScopeRef = None } ]
        }
    )

    Assert.Empty(
        check {
            catalog with
                Notices = [
                    {
                        notice with
                            Scope = NoticeScope.National
                            ScopeRef = None
                    }
                ]
        }
    )

[<Fact>]
let ``card branch must lead to a known card`` () =
    let card = {
        Slug = "start"
        PlaceSlug = Some "kasol"
        Title = "Valleys?"
        Line = "Green and quiet."
        PhotoSlug = None
        IllustrationSlug = None
        IsStart = true
        Landing = None
        Branches = [
            {
                OnAnswer = true
                ToCard = "lost"
                Priority = 0
            }
        ]
        Signals = []
    }

    Assert.True(hasError "lost" { catalog with Cards = [ card ] })

[<Fact>]
let ``unknown cluster and family both fail`` () =
    let broken = {
        catalog with
            Places = [
                {
                    place with
                        ClusterSlug = "c1"
                        FamilySlug = "f1"
                }
            ]
            Links = []
    }

    Assert.True(hasError "c1" broken)
    Assert.True(hasError "f1" broken)

let photo = {
    Slug = "kasol-river"
    PlaceSlug = "kasol"
    FilePath = "photos/kasol-river.webp"
    Alt = "The Parvati river at Kasol."
    Credit = "A photographer"
    Licence = "CC BY-SA 4.0"
    SourceUrl = "https://commons.wikimedia.org/wiki/File:Kasol.jpg"
    Sort = 1
}

[<Fact>]
let ``complete photo passes`` () =
    Assert.Empty(check { catalog with Photos = [ photo ] })

[<Fact>]
let ``photo needs a credit`` () =
    Assert.True(
        hasError "credit" {
            catalog with
                Photos = [ { photo with Credit = "" } ]
        }
    )

[<Fact>]
let ``photo needs a licence`` () =
    Assert.True(
        hasError "licence" {
            catalog with
                Photos = [ { photo with Licence = " " } ]
        }
    )

[<Fact>]
let ``photo needs a source`` () =
    Assert.True(
        hasError "source" {
            catalog with
                Photos = [ { photo with SourceUrl = "" } ]
        }
    )

[<Fact>]
let ``photo needs alt text`` () =
    Assert.True(
        hasError "alt text" {
            catalog with
                Photos = [ { photo with Alt = "" } ]
        }
    )

[<Fact>]
let ``photo must belong to a known place`` () =
    Assert.True(
        hasError "atlantis" {
            catalog with
                Photos = [ { photo with PlaceSlug = "atlantis" } ]
        }
    )

[<Fact>]
let ``card photo must exist`` () =
    let card = {
        Slug = "start"
        PlaceSlug = None
        Title = "A place"
        Line = "A line."
        PhotoSlug = Some "missing-photo"
        IllustrationSlug = None
        IsStart = true
        Landing = None
        Branches = []
        Signals = []
    }

    Assert.True(hasError "missing-photo" { catalog with Cards = [ card ] })

[<Fact>]
let ``duplicate place slug fails`` () =
    Assert.True(
        hasError "more than once" {
            catalog with
                Places = [ place; place ]
        }
    )

[<Theory>]
[<InlineData("Try the local charas.")>]
[<InlineData("GANJA is easy to find.")>]
let ``banned words in visitor copy fail`` copy =
    Assert.True(
        hasError "banned word" {
            catalog with
                Places = [ { place with Summary = copy } ]
        }
    )

[<Fact>]
let ``banned word in an unpublished draft still fails`` () =
    let draft = {
        place with
            Published = false
            Summary = "Ganja tours."
    }

    Assert.True(hasError "banned word" { catalog with Places = [ draft ] })
