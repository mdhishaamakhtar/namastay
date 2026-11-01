module Namastay.IntegrationTests.Support.Sample

open System
open Namastay.Domain.Catalog

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
    CostLow = Some 1500
    CostHigh = Some 6000
    Summary = "A valley town on the Parvati river."
    IsThisForYou = None
    GettingThere = None
    Airport = None
    Hub = false
    FoodIntro = None
    Dishes = [
        {
            Name = "River trout"
            Description = "Fried and served with rice."
            Vegetarian = false
            PhotoSlug = None
            Evidence = evidence
        }
    ]
    Eateries = [
        {
            Name = "The Bridge Cafe"
            Area = "Old town"
            GoFor = "The trout."
            CostLevel = CostLevel.Budget
            VegetarianOnly = false
            Evidence = evidence
        }
    ]
    Published = true
    Interests = [
        {
            InterestSlug = "mountains"
            Weight = 5
        }
    ]
    Months = [
        {
            Month = 5
            Rating = Rating.Best
            Note = None
            Evidence = evidence
        }
    ]
    TravellerFit = [ { Traveller = Traveller.Solo; Fit = 3 } ]
    Experiences = [
        {
            Slug = "kheerganga"
            Name = "Kheerganga"
            Description = "A hot spring trek."
            Sort = 1
            Evidence = evidence
        }
    ]
    DayPlans = [
        {
            Variant = "short"
            Day = 1
            Plan = "Walk the river."
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
                UsedFor = "Green valleys"
                Deep = "#2F5D3A"
                Mid = "#4C7A52"
                Pale = "#DCE6C8"
                TextOnDeep = "#FFFFFF"
            }
        ]
        Interests = [
            {
                Slug = "mountains"
                Name = "Mountains"
                ParentSlug = None
                Colour = None
            }
        ]
        Sources = [
            {
                Id = 1
                Url = "https://example.org"
                Title = "Source"
                Publisher = "Publisher"
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
                    Experiences = []
            }
        ]
        Links = [ link ]
}
