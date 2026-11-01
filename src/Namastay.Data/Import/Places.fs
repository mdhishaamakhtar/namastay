module Namastay.Data.Import.Places

open Namastay.Data.Sql
open Namastay.Domain.Catalog

let private upsertPlace transaction (place: Place) =
    exec
        transaction
        "insert into places (slug, cluster_slug, name, state, family_slug, shade, gate, popularity,
            days_min, days_max, cost_low, cost_high, summary, is_this_for_you, getting_there, airport, hub, food_intro, published, retired_at,
            status, last_checked)
         values (@slug, @cluster, @name, @state, @family, @shade, @gate, @popularity,
            @daysMin, @daysMax, @costLow, @costHigh, @summary, @isThisForYou, @gettingThere, @airport, @hub, @foodIntro, @published, null,
            @status, @lastChecked)
         on conflict (slug) do update set
            cluster_slug = excluded.cluster_slug, name = excluded.name, state = excluded.state,
            family_slug = excluded.family_slug, shade = excluded.shade, gate = excluded.gate,
            popularity = excluded.popularity, days_min = excluded.days_min, days_max = excluded.days_max,
            cost_low = excluded.cost_low, cost_high = excluded.cost_high, summary = excluded.summary,
            is_this_for_you = excluded.is_this_for_you, getting_there = excluded.getting_there,
            airport = excluded.airport, hub = excluded.hub, food_intro = excluded.food_intro,
            published = excluded.published,
            retired_at = null, status = excluded.status, last_checked = excluded.last_checked"
        {|
            slug = place.Slug
            cluster = place.ClusterSlug
            name = place.Name
            state = place.State
            family = place.FamilySlug
            shade = Encode.shade place.Shade
            gate = (Encode.gate place.Gate)
            popularity = (Encode.popularity place.Popularity)
            daysMin = place.DaysMin
            daysMax = place.DaysMax
            costLow = place.CostLow
            costHigh = place.CostHigh
            summary = place.Summary
            isThisForYou = place.IsThisForYou
            gettingThere = place.GettingThere
            airport = place.Airport
            hub = place.Hub
            foodIntro = place.FoodIntro
            published = place.Published
            status = Encode.status place.Evidence.Status
            lastChecked = place.Evidence.LastChecked
        |}
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
            "dishes"
            "eateries"
        ] do
        exec transaction $"delete from {table} where place_slug = @slug" {| slug = slug |}
        |> ignore

let private insertInterests transaction (place: Place) =
    for interest in place.Interests do
        exec
            transaction
            "insert into place_interests (place_slug, interest_slug, weight) values (@place, @interest, @weight)"
            {|
                place = place.Slug
                interest = interest.InterestSlug
                weight = interest.Weight
            |}
        |> ignore

let private insertMonths transaction (place: Place) =
    for month in place.Months do
        exec
            transaction
            "insert into place_months (place_slug, month, rating, note, status, last_checked)
             values (@place, @month, @rating, @note, @status, @lastChecked)"
            {|
                place = place.Slug
                month = month.Month
                rating = (Encode.rating month.Rating)
                note = month.Note
                status = Encode.status month.Evidence.Status
                lastChecked = month.Evidence.LastChecked
            |}
        |> ignore

        linkSources transaction "place_months" $"{place.Slug}:{month.Month}" month.Evidence

let private insertFit transaction (place: Place) =
    for fit in place.TravellerFit do
        exec transaction "insert into traveller_fit (place_slug, traveller, fit) values (@place, @traveller, @fit)" {|
            place = place.Slug
            traveller = (Encode.traveller fit.Traveller)
            fit = fit.Fit
        |}
        |> ignore

let private insertDayPlans transaction (place: Place) =
    for plan in place.DayPlans do
        exec
            transaction
            "insert into day_plans (place_slug, variant, day, plan, status, last_checked)
             values (@place, @variant, @day, @plan, @status, @lastChecked)"
            {|
                place = place.Slug
                variant = plan.Variant
                day = plan.Day
                plan = plan.Plan
                status = Encode.status plan.Evidence.Status
                lastChecked = plan.Evidence.LastChecked
            |}
        |> ignore

        linkSources transaction "day_plans" $"{place.Slug}:{plan.Variant}:{plan.Day}" plan.Evidence

let private insertFood transaction (place: Place) =
    for dish in place.Dishes do
        exec
            transaction
            "insert into dishes (place_slug, name, description, vegetarian, photo_slug, rank, status, last_checked)
             values (@place, @name, @description, @vegetarian, @photo, @rank, @status, @lastChecked)"
            {|
                place = place.Slug
                name = dish.Name
                description = dish.Description
                vegetarian = dish.Vegetarian
                photo = dish.PhotoSlug
                rank = dish.Rank
                status = Encode.status dish.Evidence.Status
                lastChecked = dish.Evidence.LastChecked
            |}
        |> ignore

        linkSources transaction "dishes" $"{place.Slug}:{dish.Name}" dish.Evidence

    for eatery in place.Eateries do
        exec
            transaction
            "insert into eateries (place_slug, name, area, go_for, cost_level, vegetarian_only, status, last_checked)
             values (@place, @name, @area, @goFor, @costLevel, @vegetarianOnly, @status, @lastChecked)"
            {|
                place = place.Slug
                name = eatery.Name
                area = eatery.Area
                goFor = eatery.GoFor
                costLevel = Encode.costLevel eatery.CostLevel
                vegetarianOnly = eatery.VegetarianOnly
                status = Encode.status eatery.Evidence.Status
                lastChecked = eatery.Evidence.LastChecked
            |}
        |> ignore

        linkSources transaction "eateries" $"{place.Slug}:{eatery.Name}" eatery.Evidence

let private insertStayAreas transaction (place: Place) =
    for area in place.StayAreas do
        exec
            transaction
            "insert into stay_areas (place_slug, name, cost_level, note, status, last_checked)
             values (@place, @name, @costLevel, @note, @status, @lastChecked)"
            {|
                place = place.Slug
                name = area.Name
                costLevel = (Encode.costLevel area.CostLevel)
                note = area.Note
                status = Encode.status area.Evidence.Status
                lastChecked = area.Evidence.LastChecked
            |}
        |> ignore

        linkSources transaction "stay_areas" $"{place.Slug}:{area.Name}" area.Evidence

let private upsertExperiences transaction (place: Place) =
    exec
        transaction
        "update experiences set retired_at = now()
         where place_slug = @place and retired_at is null and not (slug = any(@slugs))"
        {|
            place = place.Slug
            slugs = (place.Experiences |> List.map _.Slug |> List.toArray)
        |}
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
            {|
                slug = experience.Slug
                place = place.Slug
                name = experience.Name
                description = experience.Description
                sort = experience.Sort
                status = Encode.status experience.Evidence.Status
                lastChecked = experience.Evidence.LastChecked
            |}
        |> ignore

        linkSources transaction "experiences" experience.Slug experience.Evidence

let private retireMissing transaction (catalog: Catalog) =
    exec transaction "update places set retired_at = now() where retired_at is null and not (slug = any(@slugs))" {|
        slugs = (catalog.Places |> List.map _.Slug |> List.toArray)
    |}
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
        insertFood transaction place
        upsertExperiences transaction place

    if catalog.Complete then
        retireMissing transaction catalog
