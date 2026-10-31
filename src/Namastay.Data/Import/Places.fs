module Namastay.Data.Import.Places

open Namastay.Data.Sql
open Namastay.Domain.Catalog

let private facts (evidence: Evidence) = [
    arg "status" (Encode.status evidence.Status)
    arg "lastChecked" evidence.LastChecked
]

let private upsertPlace transaction (place: Place) =
    exec
        transaction
        "insert into places (slug, cluster_slug, name, state, family_slug, shade, gate, popularity,
            days_min, days_max, cost_low, cost_high, summary, is_this_for_you, published, retired_at,
            status, last_checked)
         values (@slug, @cluster, @name, @state, @family, @shade, @gate, @popularity,
            @daysMin, @daysMax, @costLow, @costHigh, @summary, @isThisForYou, @published, null,
            @status, @lastChecked)
         on conflict (slug) do update set
            cluster_slug = excluded.cluster_slug, name = excluded.name, state = excluded.state,
            family_slug = excluded.family_slug, shade = excluded.shade, gate = excluded.gate,
            popularity = excluded.popularity, days_min = excluded.days_min, days_max = excluded.days_max,
            cost_low = excluded.cost_low, cost_high = excluded.cost_high, summary = excluded.summary,
            is_this_for_you = excluded.is_this_for_you, published = excluded.published,
            retired_at = null, status = excluded.status, last_checked = excluded.last_checked"
        ([
            arg "slug" place.Slug
            arg "cluster" place.ClusterSlug
            arg "name" place.Name
            arg "state" place.State
            arg "family" place.FamilySlug
            arg "shade" place.Shade
            arg "gate" (Encode.gate place.Gate)
            arg "popularity" (Encode.popularity place.Popularity)
            arg "daysMin" place.DaysMin
            arg "daysMax" place.DaysMax
            opt "costLow" place.CostLow
            opt "costHigh" place.CostHigh
            arg "summary" place.Summary
            opt "isThisForYou" place.IsThisForYou
            arg "published" place.Published
         ]
         @ facts place.Evidence)
    |> ignore

    linkSources transaction "places" place.Slug place.Evidence

let private clearChildren transaction (slug: string) =
    for table in
        [
            "place_interests"
            "place_months"
            "traveller_fit"
            "day_plans"
            "stay_areas"
        ] do
        exec transaction $"delete from {table} where place_slug = @slug" [ arg "slug" slug ]
        |> ignore

let private insertInterests transaction (place: Place) =
    for interest in place.Interests do
        exec
            transaction
            "insert into place_interests (place_slug, interest_slug, weight) values (@place, @interest, @weight)"
            [
                arg "place" place.Slug
                arg "interest" interest.InterestSlug
                arg "weight" interest.Weight
            ]
        |> ignore

let private insertMonths transaction (place: Place) =
    for month in place.Months do
        exec
            transaction
            "insert into place_months (place_slug, month, rating, note, status, last_checked)
             values (@place, @month, @rating, @note, @status, @lastChecked)"
            ([
                arg "place" place.Slug
                arg "month" month.Month
                arg "rating" (Encode.rating month.Rating)
                opt "note" month.Note
             ]
             @ facts month.Evidence)
        |> ignore

        linkSources transaction "place_months" $"{place.Slug}:{month.Month}" month.Evidence

let private insertFit transaction (place: Place) =
    for fit in place.TravellerFit do
        exec transaction "insert into traveller_fit (place_slug, traveller, fit) values (@place, @traveller, @fit)" [
            arg "place" place.Slug
            arg "traveller" (Encode.traveller fit.Traveller)
            arg "fit" fit.Fit
        ]
        |> ignore

let private insertDayPlans transaction (place: Place) =
    for plan in place.DayPlans do
        exec
            transaction
            "insert into day_plans (place_slug, variant, day, plan, status, last_checked)
             values (@place, @variant, @day, @plan, @status, @lastChecked)"
            ([
                arg "place" place.Slug
                arg "variant" plan.Variant
                arg "day" plan.Day
                arg "plan" plan.Plan
             ]
             @ facts plan.Evidence)
        |> ignore

        linkSources transaction "day_plans" $"{place.Slug}:{plan.Variant}:{plan.Day}" plan.Evidence

let private insertStayAreas transaction (place: Place) =
    for area in place.StayAreas do
        exec
            transaction
            "insert into stay_areas (place_slug, name, cost_level, note, status, last_checked)
             values (@place, @name, @costLevel, @note, @status, @lastChecked)"
            ([
                arg "place" place.Slug
                arg "name" area.Name
                arg "costLevel" (Encode.costLevel area.CostLevel)
                opt "note" area.Note
             ]
             @ facts area.Evidence)
        |> ignore

        linkSources transaction "stay_areas" $"{place.Slug}:{area.Name}" area.Evidence

let private upsertExperiences transaction (place: Place) =
    exec
        transaction
        "update experiences set retired_at = now()
         where place_slug = @place and retired_at is null and not (slug = any(@slugs))"
        [
            arg "place" place.Slug
            arg "slugs" (place.Experiences |> List.map _.Slug |> List.toArray)
        ]
    |> ignore

    for experience in place.Experiences do
        exec
            transaction
            "insert into experiences (slug, place_slug, name, description, sort, status, last_checked)
             values (@slug, @place, @name, @description, @sort, @status, @lastChecked)
             on conflict (slug) do update set
                place_slug = excluded.place_slug, name = excluded.name, description = excluded.description,
                sort = excluded.sort, status = excluded.status, last_checked = excluded.last_checked,
                retired_at = null"
            ([
                arg "slug" experience.Slug
                arg "place" place.Slug
                arg "name" experience.Name
                arg "description" experience.Description
                arg "sort" experience.Sort
             ]
             @ facts experience.Evidence)
        |> ignore

        linkSources transaction "experiences" experience.Slug experience.Evidence

let private retireMissing transaction (catalog: Catalog) =
    exec transaction "update places set retired_at = now() where retired_at is null and not (slug = any(@slugs))" [
        arg "slugs" (catalog.Places |> List.map _.Slug |> List.toArray)
    ]
    |> ignore

let apply transaction (catalog: Catalog) =
    for place in catalog.Places do
        upsertPlace transaction place
        clearChildren transaction place.Slug
        insertInterests transaction place
        insertMonths transaction place
        insertFit transaction place
        insertDayPlans transaction place
        insertStayAreas transaction place
        upsertExperiences transaction place

    if catalog.Complete then
        retireMissing transaction catalog
