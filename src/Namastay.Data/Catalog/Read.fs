module Namastay.Data.Catalog.Read

open System
open System.Threading.Tasks
open Npgsql
open Namastay.Data.Catalog.Rows
open Namastay.Data.Sql
open Namastay.Domain.Catalog

let private evidence status (checkedOn: DateTime) = {
    Status = Decode.status status
    LastChecked = DateOnly.FromDateTime checkedOn
    SourceIds = []
}

let private ofPlace (table: string) =
    $"select * from {table} where place_slug in (select slug from places where retired_at is null)"

let private matching (slug: string) (rows: 'Row list) (key: 'Row -> string) =
    rows |> List.filter (fun row -> key row = slug)

let private loadPlaces source =
    task {
        let! places =
            query<PlaceRow> source "select * from places where retired_at is null order by slug" null

        let! interests = query<InterestRow> source (ofPlace "place_interests") null
        let! months = query<MonthRow> source (ofPlace "place_months") null
        let! fits = query<FitRow> source (ofPlace "traveller_fit") null
        let! days = query<DayRow> source (ofPlace "day_plans") null
        let! stays = query<StayRow> source (ofPlace "stay_areas") null

        return
            places
            |> List.map (fun place -> {
                Slug = place.Slug
                ClusterSlug = place.ClusterSlug
                Name = place.Name
                State = place.State
                FamilySlug = place.FamilySlug
                Shade = Decode.shade place.Shade
                Gate = Decode.gate place.Gate
                Popularity = Decode.popularity place.Popularity
                DaysMin = place.DaysMin
                DaysMax = place.DaysMax
                CostLow = place.CostLow
                CostHigh = place.CostHigh
                Summary = place.Summary
                IsThisForYou = None
                GettingThere = None
                Airport = place.Airport
                Hub = place.Hub
                FoodIntro = None
                Dishes = []
                Eateries = []
                Published = place.Published
                Interests =
                    matching place.Slug interests _.PlaceSlug
                    |> List.map (fun row -> {
                        InterestSlug = row.InterestSlug
                        Weight = row.Weight
                    })
                Months =
                    matching place.Slug months _.PlaceSlug
                    |> List.map (fun row -> {
                        Month = row.Month
                        Rating = Decode.rating row.Rating
                        Note = row.Note
                        Evidence = evidence row.Status row.LastChecked
                    })
                TravellerFit =
                    matching place.Slug fits _.PlaceSlug
                    |> List.map (fun row -> {
                        Traveller = Decode.traveller row.Traveller
                        Fit = row.Fit
                    })
                Experiences = []
                DayPlans =
                    matching place.Slug days _.PlaceSlug
                    |> List.filter (fun row -> row.Status <> "unverified")
                    |> List.map (fun row -> {
                        Variant = row.Variant
                        Day = row.Day
                        Plan = row.Plan
                        Evidence = evidence row.Status row.LastChecked
                    })
                StayAreas =
                    matching place.Slug stays _.PlaceSlug
                    |> List.map (fun row -> {
                        Name = row.Name
                        CostLevel = Decode.costLevel row.CostLevel
                        Note = None
                        Evidence = evidence row.Status row.LastChecked
                    })
                Evidence = evidence place.Status place.LastChecked
            })
    }

let private loadLinks source =
    task {
        let! links =
            query<LinkRow> source "select * from transport_links where retired_at is null order by slug" null

        return
            links
            |> List.map (fun link -> {
                Slug = link.Slug
                FromSlug = link.FromSlug
                ToSlug = link.ToSlug
                Mode = Decode.mode link.Mode
                MinutesMin = link.MinutesMin
                MinutesMax = link.MinutesMax
                DistanceKm = None
                RunDays = None
                ValidFrom = None
                ValidTo = None
                ComfortNote = link.ComfortNote
                Trains = []
                Evidence = evidence link.Status link.LastChecked
            })
    }

let private loadNotices source =
    task {
        let! notices =
            query<NoticeRow>
                source
                "select * from notices
                 where (valid_from is null or valid_from <= current_date)
                   and (valid_to is null or valid_to >= current_date)
                 order by slug"
                null

        return
            notices
            |> List.map (fun notice -> {
                Slug = notice.Slug
                Scope = Decode.scope notice.Scope
                ScopeRef = notice.ScopeRef
                Kind = Decode.noticeKind notice.Kind
                Severity = Decode.severity notice.Severity
                Body = notice.Body
                ValidFrom = None
                ValidTo = None
                Evidence = evidence notice.Status notice.LastChecked
            })
    }

let private loadPhotos source =
    task {
        let! photos =
            query<PhotoRow>
                source
                "select slug, place_slug, file_path, alt, credit, licence, source_url, sort from photos"
                null

        return
            photos
            |> List.map (fun photo -> {
                Slug = photo.Slug
                PlaceSlug = photo.PlaceSlug
                FilePath = photo.FilePath
                Alt = photo.Alt
                Credit = photo.Credit
                Licence = photo.Licence
                SourceUrl = photo.SourceUrl
                Sort = photo.Sort
            })
    }

let private loadCards source =
    task {
        let! cards =
            query<CardRow> source "select slug, place_slug from cards order by slug" null

        let! signals =
            query<SignalRow> source "select card_slug, signal, weight from card_signals" null

        return
            cards
            |> List.map (fun card -> {
                Slug = card.Slug
                PlaceSlug = card.PlaceSlug
                Title = ""
                Line = ""
                PhotoSlug = None
                IllustrationSlug = None
                IsStart = false
                Landing = None
                Branches = []
                Signals =
                    matching card.Slug signals _.CardSlug
                    |> List.map (fun row -> {
                        Signal = row.Signal
                        Weight = row.Weight
                    })
            })
    }

/// Everything the trip builder needs, read fresh from Postgres.
let load (source: NpgsqlDataSource) : Task<Catalog> =
    task {
        let! places = loadPlaces source
        let! links = loadLinks source
        let! notices = loadNotices source
        let! cards = loadCards source
        let! photos = loadPhotos source
        let! regions = query<NamedRow> source "select slug, name from regions" null

        let! clusters =
            query<ClusterRow> source "select slug, region_slug, name from clusters" null

        let! families = query<FamilyRow> source "select * from colour_families" null
        let! interests = query<NamedRow> source "select slug, name from interests" null
        let! exclusions = query<NamedRow> source "select slug, name from exclusions" null

        return {
            Catalog.empty with
                Regions =
                    regions
                    |> List.map (fun row -> {
                        Slug = row.Slug
                        Name = row.Name
                        Colour = None
                        Sort = None
                    })
                Clusters =
                    clusters
                    |> List.map (fun row -> {
                        Slug = row.Slug
                        RegionSlug = row.RegionSlug
                        Name = row.Name
                    })
                Families =
                    families
                    |> List.map (fun row -> {
                        Slug = row.Slug
                        Name = row.Name
                        UsedFor = row.UsedFor
                        Deep = row.Deep
                        Mid = row.Mid
                        Pale = row.Pale
                        TextOnDeep = row.TextOnDeep
                    })
                Interests =
                    interests
                    |> List.map (fun row -> {
                        Slug = row.Slug
                        Name = row.Name
                        ParentSlug = None
                        Colour = None
                    })
                Exclusions =
                    exclusions
                    |> List.map (fun row -> {
                        Slug = row.Slug
                        Name = row.Name
                        Reason = ""
                    })
                Places = places
                Links = links
                Notices = notices
                Photos = photos
                Cards = cards
        }
    }
