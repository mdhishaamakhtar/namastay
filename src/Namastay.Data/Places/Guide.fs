module Namastay.Data.Places.Guide

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql
open Namastay.Shared.Contracts

let private hedgedSql = "status in ('one_source', 'conflict')"

let private headSql =
    "select p.slug, p.name, p.state, r.name, p.summary, p.is_this_for_you, p.days_min, p.days_max,
            p.cost_low, p.cost_high, p.gate, f.deep, f.mid, f.pale, f.text_on_deep, p.last_checked::text
     from places p
     join clusters c on c.slug = p.cluster_slug
     join regions r on r.slug = c.region_slug
     join colour_families f on f.slug = p.family_slug
     where p.slug = @slug and p.published and p.retired_at is null"

let private rating =
    function
    | "best" -> MonthRating.Best
    | "ok" -> MonthRating.Ok
    | "avoid" -> MonthRating.Avoid
    | "closed" -> MonthRating.Closed
    | _ -> MonthRating.Unknown

let private months source slug =
    query
        source
        "select month, case when status = 'unverified' then 'unknown' else rating end,
                case when status = 'unverified' then null else note end
         from place_months where place_slug = @slug order by month"
        [ arg "slug" slug ]
        (fun reader -> {
            Month = reader.GetInt32 0
            Rating = rating (reader.GetString 1)
            Note = text reader 2
        })

let private experiences source slug =
    query
        source
        $"select name, description, {hedgedSql} from experiences
          where place_slug = @slug and status <> 'unverified' and retired_at is null
          order by sort, name"
        [ arg "slug" slug ]
        (fun reader -> {
            GuideExperience.Name = reader.GetString 0
            Description = reader.GetString 1
            Hedged = reader.GetBoolean 2
        })

let private dayPlans source slug =
    query
        source
        "select variant, day, plan from day_plans
         where place_slug = @slug and status <> 'unverified' order by variant, day"
        [ arg "slug" slug ]
        (fun reader -> {
            Variant = reader.GetString 0
            Day = reader.GetInt32 1
            Plan = reader.GetString 2
        })

let private stayAreas source slug =
    query
        source
        $"select name, cost_level, note, {hedgedSql} from stay_areas
          where place_slug = @slug and status <> 'unverified' order by name"
        [ arg "slug" slug ]
        (fun reader -> {
            GuideStay.Name = reader.GetString 0
            CostLevel = reader.GetString 1
            Note = text reader 2
            Hedged = reader.GetBoolean 3
        })

let private notices source slug state =
    query
        source
        $"select kind, severity, body, {hedgedSql} from notices
          where status <> 'unverified'
            and (valid_from is null or valid_from <= current_date)
            and (valid_to is null or valid_to >= current_date)
            and (scope = 'national'
                 or (scope = 'state' and scope_ref = @state)
                 or (scope = 'place' and scope_ref = @slug))
          order by slug"
        [ arg "slug" slug; arg "state" state ]
        (fun reader -> {
            Kind = reader.GetString 0
            Severity = reader.GetString 1
            Body = reader.GetString 2
            Hedged = reader.GetBoolean 3
        })

let private photos source slug =
    query
        source
        "select file_path, alt, credit, licence, source_url from photos where place_slug = @slug order by sort, slug"
        [ arg "slug" slug ]
        (fun reader -> {
            Url = "/media/" + reader.GetString 0
            Alt = reader.GetString 1
            Credit = reader.GetString 2
            Licence = reader.GetString 3
            SourceUrl = reader.GetString 4
        })

let private suits source slug =
    query
        source
        "select traveller from traveller_fit where place_slug = @slug and fit >= 2 order by fit desc, traveller"
        [ arg "slug" slug ]
        (fun reader -> reader.GetString 0)

let private readHead (reader: NpgsqlDataReader) : PlaceGuide = {
    Slug = reader.GetString 0
    Name = reader.GetString 1
    State = reader.GetString 2
    Region = reader.GetString 3
    Summary = reader.GetString 4
    IsThisForYou = text reader 5
    DaysMin = reader.GetInt32 6
    DaysMax = reader.GetInt32 7
    CostLow = number reader 8
    CostHigh = number reader 9
    Gate = reader.GetString 10
    Colours = {
        Deep = reader.GetString 11
        Mid = reader.GetString 12
        Pale = reader.GetString 13
        TextOnDeep = reader.GetString 14
    }
    Suits = []
    Months = []
    Experiences = []
    DayPlans = []
    StayAreas = []
    Notices = []
    Photos = []
    LastChecked = reader.GetString 15
}

/// Only published, unretired places have a guide. Unverified facts are never included.
let tryLoad (source: NpgsqlDataSource) (slug: string) : Task<PlaceGuide option> =
    task {
        let! heads = query source headSql [ arg "slug" slug ] readHead

        match heads with
        | [] -> return None
        | head :: _ ->
            let! suits = suits source slug
            let! months = months source slug
            let! experiences = experiences source slug
            let! dayPlans = dayPlans source slug
            let! stayAreas = stayAreas source slug
            let! notices = notices source slug head.State
            let! photos = photos source slug

            return
                Some {
                    head with
                        Suits = suits
                        Months = months
                        Experiences = experiences
                        DayPlans = dayPlans
                        StayAreas = stayAreas
                        Notices = notices
                        Photos = photos
                }
    }
