module Namastay.Data.Import.Transport

open Namastay.Data.Sql
open Namastay.Domain.Catalog

let private upsertLink transaction (link: TransportLink) =
    exec
        transaction
        "insert into transport_links (slug, from_slug, to_slug, mode, minutes_min, minutes_max, distance_km,
            run_days, valid_from, valid_to, comfort_note, status, last_checked, retired_at)
         values (@slug, @fromSlug, @toSlug, @mode, @minutesMin, @minutesMax, @distanceKm,
            @runDays, @validFrom, @validTo, @comfortNote, @status, @lastChecked, null)
         on conflict (slug) do update set
            from_slug = excluded.from_slug, to_slug = excluded.to_slug, mode = excluded.mode,
            minutes_min = excluded.minutes_min, minutes_max = excluded.minutes_max,
            distance_km = excluded.distance_km, run_days = excluded.run_days,
            valid_from = excluded.valid_from, valid_to = excluded.valid_to,
            comfort_note = excluded.comfort_note, status = excluded.status,
            last_checked = excluded.last_checked, retired_at = null"
        {|
            slug = link.Slug
            fromSlug = link.FromSlug
            toSlug = link.ToSlug
            mode = (Encode.mode link.Mode)
            minutesMin = link.MinutesMin
            minutesMax = link.MinutesMax
            distanceKm = link.DistanceKm
            runDays = link.RunDays
            validFrom = link.ValidFrom
            validTo = link.ValidTo
            comfortNote = link.ComfortNote
            status = (Encode.status link.Evidence.Status)
            lastChecked = link.Evidence.LastChecked
        |}
    |> ignore

    linkSources transaction "transport_links" link.Slug link.Evidence

let private replaceTrains transaction (link: TransportLink) =
    exec transaction "delete from trains where link_slug = @link" {| link = link.Slug |}
    |> ignore

    for train in link.Trains do
        exec
            transaction
            "insert into trains (link_slug, number, departs, arrives, run_days, source_note, status, last_checked)
             values (@link, @number, @departs, @arrives, @runDays, @sourceNote, @status, @lastChecked)"
            {|
                link = link.Slug
                number = train.Number
                departs = train.Departs
                arrives = train.Arrives
                runDays = train.RunDays
                sourceNote = train.SourceNote
                status = (Encode.status train.Evidence.Status)
                lastChecked = train.Evidence.LastChecked
            |}
        |> ignore

        linkSources transaction "trains" $"{link.Slug}:{train.Number}" train.Evidence

let private retireMissing transaction (catalog: Catalog) =
    exec
        transaction
        "update transport_links set retired_at = now() where retired_at is null and not (slug = any(@slugs))"
        {|
            slugs = (catalog.Links |> List.map _.Slug |> List.toArray)
        |}
    |> ignore

let apply transaction (catalog: Catalog) =
    for link in catalog.Links do
        upsertLink transaction link
        replaceTrains transaction link

    if catalog.Complete then
        retireMissing transaction catalog
