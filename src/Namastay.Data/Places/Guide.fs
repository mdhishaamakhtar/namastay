module Namastay.Data.Places.Guide

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Catalog
open Namastay.Data.Places.Rows
open Namastay.Data.Sql
open Namastay.Domain.Catalog
open Namastay.Shared
open Namastay.Shared.Contracts

let private hedged = "status in ('one_source', 'conflict') as hedged"

let private headSql =
    "select p.slug, p.name, p.state, r.name as region, p.summary, p.is_this_for_you, p.getting_there, p.food_intro, mark.file_path as mark_path, p.days_min, p.days_max,
            p.cost_low, p.cost_high, p.gate, f.deep, f.mid, f.pale, f.text_on_deep,
            p.last_checked::text as last_checked
     from places p
     join clusters c on c.slug = p.cluster_slug
     join regions r on r.slug = c.region_slug
     join colour_families f on f.slug = p.family_slug
     left join illustrations mark on mark.slug = p.slug || '-mark'
     where p.slug = @slug and p.published and p.retired_at is null"

let private monthsSql =
    "select month, case when status = 'unverified' then 'unknown' else rating end as rating,
            case when status = 'unverified' then null else note end as note
     from place_months where place_slug = @slug order by month"

let private experiencesSql =
    $"select name, description, {hedged} from experiences
      where place_slug = @slug and status <> 'unverified' and retired_at is null
      order by sort, name"

let private dayPlansSql =
    "select variant, day, plan from day_plans
     where place_slug = @slug and status <> 'unverified' order by variant, day"

let private stayAreasSql =
    $"select name, cost_level, note, {hedged} from stay_areas
      where place_slug = @slug and status <> 'unverified' order by name"

let private dishesSql =
    "select name, description, vegetarian from dishes
     where place_slug = @slug and status <> 'unverified' order by name"

let private eateriesSql =
    "select name, area, go_for, cost_level, vegetarian_only from eateries
     where place_slug = @slug and status <> 'unverified' order by name"

let private costLabel (level: string) =
    match level with
    | "budget" -> "Cheap eats"
    | "luxury" -> "A treat"
    | _ -> "Mid-range"

let private noticesSql =
    $"select kind, severity, body, {hedged} from notices
      where status <> 'unverified'
        and (valid_from is null or valid_from <= current_date)
        and (valid_to is null or valid_to >= current_date)
        and (scope = 'national'
             or (scope = 'state' and scope_ref = @state)
             or (scope = 'place' and scope_ref = @slug))
      order by slug"

/// Dish photos sort from 1000, so a guide's gallery stays about the place.
let private photosSql =
    "select file_path, alt, credit, licence, source_url from photos where place_slug = @slug and sort < 1000 order by sort, slug"

let private suitsSql =
    "select traveller from traveller_fit where place_slug = @slug and fit >= 2 order by fit desc, traveller"

let private toGuide (head: HeadRow) : PlaceGuide = {
    Slug = head.Slug
    Name = head.Name
    State = head.State
    Region = head.Region
    Summary = head.Summary
    IsThisForYou = head.IsThisForYou
    GettingThere = head.GettingThere
    MarkUrl = head.MarkPath |> Option.map Routes.mediaUrl
    DaysMin = head.DaysMin
    DaysMax = head.DaysMax
    CostLow = head.CostLow
    CostHigh = head.CostHigh
    Access = head.Gate |> Decode.gate |> ContractMapping.access
    Colours = {
        Deep = head.Deep
        Mid = head.Mid
        Pale = head.Pale
        TextOnDeep = head.TextOnDeep
    }
    Suits = []
    Months = []
    Experiences = []
    FoodIntro = head.FoodIntro
    Dishes = []
    Eateries = []
    DayPlans = []
    StayAreas = []
    Notices = []
    Photos = []
    LastChecked = head.LastChecked
}

/// Only published, unretired places have a guide. Unverified facts are never included.
let tryLoad (source: NpgsqlDataSource) (slug: string) : Task<PlaceGuide option> =
    task {
        let bySlug = {| slug = slug |}
        let! heads = query<HeadRow> source headSql bySlug

        match heads with
        | [] -> return None
        | head :: _ ->
            let! suits = query<string> source suitsSql bySlug
            let! months = query<MonthRow> source monthsSql bySlug
            let! experiences = query<ExperienceRow> source experiencesSql bySlug
            let! dayPlans = query<DayRow> source dayPlansSql bySlug
            let! stayAreas = query<StayRow> source stayAreasSql bySlug
            let! dishes = query<DishRow> source dishesSql bySlug
            let! eateries = query<EateryRow> source eateriesSql bySlug

            let! notices =
                query<NoticeRow> source noticesSql {| slug = slug; state = head.State |}

            let! photos = query<PhotoRow> source photosSql bySlug

            return
                Some {
                    toGuide head with
                        Suits = suits |> List.map (Decode.traveller >> ContractMapping.travellerType)
                        Months =
                            months
                            |> List.map (fun row -> {
                                Month = row.Month
                                Rating = row.Rating |> Decode.rating |> ContractMapping.monthRating
                                Note = row.Note
                            })
                        Experiences =
                            experiences
                            |> List.map (fun row -> {
                                GuideExperience.Name = row.Name
                                Description = row.Description
                                Hedged = row.Hedged
                            })
                        DayPlans =
                            dayPlans
                            |> List.map (fun row -> {
                                Variant = row.Variant
                                Day = row.Day
                                Plan = row.Plan
                            })
                        Dishes =
                            dishes
                            |> List.map (fun row -> {
                                Name = row.Name
                                Description = row.Description
                                Vegetarian = row.Vegetarian
                            })
                        Eateries =
                            eateries
                            |> List.map (fun row -> {
                                Name = row.Name
                                Area = row.Area
                                GoFor = row.GoFor
                                Cost = costLabel row.CostLevel
                                VegetarianOnly = row.VegetarianOnly
                            })
                        StayAreas =
                            stayAreas
                            |> List.map (fun row -> {
                                GuideStay.Name = row.Name
                                CostLevel = row.CostLevel |> Decode.costLevel |> ContractMapping.budgetLevel
                                Note = row.Note
                                Hedged = row.Hedged
                            })
                        Notices =
                            notices
                            |> List.map (fun row -> {
                                Kind = row.Kind |> Decode.noticeKind |> ContractMapping.noticeKind
                                Severity = row.Severity |> Decode.severity |> ContractMapping.severity
                                Body = row.Body
                                Hedged = row.Hedged
                            })
                        Photos =
                            photos
                            |> List.map (fun row -> {
                                Url = Routes.mediaUrl row.FilePath
                                Alt = row.Alt
                                Credit = row.Credit
                                Licence = row.Licence
                                SourceUrl = row.SourceUrl
                            })
                }
    }

let publishedSlugs (source: NpgsqlDataSource) : Task<string list> =
    query<string> source "select slug from places where published and retired_at is null order by slug" null
