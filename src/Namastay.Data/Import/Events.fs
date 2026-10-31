module Namastay.Data.Import.Events

open Namastay.Data.Sql
open Namastay.Domain.Catalog

let private upsertFestival transaction (festival: Festival) =
    exec
        transaction
        "insert into festivals (slug, name, place_slug, date_rule, visitor_notes, status, last_checked)
         values (@slug, @name, @place, @dateRule, @visitorNotes, @status, @lastChecked)
         on conflict (slug) do update set
            name = excluded.name, place_slug = excluded.place_slug, date_rule = excluded.date_rule,
            visitor_notes = excluded.visitor_notes, status = excluded.status, last_checked = excluded.last_checked"
        {|
            slug = festival.Slug
            name = festival.Name
            place = festival.PlaceSlug
            dateRule = festival.DateRule
            visitorNotes = festival.VisitorNotes
            status = (Encode.status festival.Evidence.Status)
            lastChecked = festival.Evidence.LastChecked
        |}
    |> ignore

    linkSources transaction "festivals" festival.Slug festival.Evidence

    exec transaction "delete from festival_dates where festival_slug = @slug" {| slug = festival.Slug |}
    |> ignore

    for date in festival.Dates do
        exec
            transaction
            "insert into festival_dates (festival_slug, year, starts, ends, is_moon_dependent)
             values (@slug, @year, @starts, @ends, @moon)"
            {|
                slug = festival.Slug
                year = date.Year
                starts = date.Starts
                ends = date.Ends
                moon = date.IsMoonDependent
            |}
        |> ignore

let private upsertNotice transaction (notice: Notice) =
    exec
        transaction
        "insert into notices (slug, scope, scope_ref, kind, severity, body, valid_from, valid_to, status, last_checked)
         values (@slug, @scope, @scopeRef, @kind, @severity, @body, @validFrom, @validTo, @status, @lastChecked)
         on conflict (slug) do update set
            scope = excluded.scope, scope_ref = excluded.scope_ref, kind = excluded.kind,
            severity = excluded.severity, body = excluded.body, valid_from = excluded.valid_from,
            valid_to = excluded.valid_to, status = excluded.status, last_checked = excluded.last_checked"
        {|
            slug = notice.Slug
            scope = (Encode.scope notice.Scope)
            scopeRef = notice.ScopeRef
            kind = notice.Kind
            severity = (Encode.severity notice.Severity)
            body = notice.Body
            validFrom = notice.ValidFrom
            validTo = notice.ValidTo
            status = (Encode.status notice.Evidence.Status)
            lastChecked = notice.Evidence.LastChecked
        |}
    |> ignore

    linkSources transaction "notices" notice.Slug notice.Evidence

let private upsertExclusion transaction (exclusion: Exclusion) =
    exec
        transaction
        "insert into exclusions (slug, name, reason) values (@slug, @name, @reason)
         on conflict (slug) do update set name = excluded.name, reason = excluded.reason"
        {|
            slug = exclusion.Slug
            name = exclusion.Name
            reason = exclusion.Reason
        |}
    |> ignore

let apply transaction (catalog: Catalog) =
    catalog.Festivals |> List.iter (upsertFestival transaction)
    catalog.Notices |> List.iter (upsertNotice transaction)
    catalog.Exclusions |> List.iter (upsertExclusion transaction)
