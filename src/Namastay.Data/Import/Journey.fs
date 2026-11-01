module Namastay.Data.Import.Journey

open Namastay.Data.Sql
open Namastay.Domain.Catalog

let private upsertIllustration transaction (illustration: Illustration) =
    exec
        transaction
        "insert into illustrations (slug, file_path, alt, brief, status)
         values (@slug, @filePath, @alt, @brief, @status)
         on conflict (slug) do update set
            file_path = excluded.file_path, alt = excluded.alt, brief = excluded.brief, status = excluded.status"
        {|
            slug = illustration.Slug
            filePath = illustration.FilePath
            alt = illustration.Alt
            brief = illustration.Brief
            status = Encode.illustrationStatus illustration.Status
        |}
    |> ignore

let private upsertPhoto transaction (photo: Photo) =
    exec
        transaction
        "insert into photos (slug, place_slug, file_path, alt, credit, licence, source_url, sort)
         values (@slug, @place, @filePath, @alt, @credit, @licence, @sourceUrl, @sort)
         on conflict (slug) do update set
            place_slug = excluded.place_slug, file_path = excluded.file_path, alt = excluded.alt,
            credit = excluded.credit, licence = excluded.licence, source_url = excluded.source_url,
            sort = excluded.sort"
        {|
            slug = photo.Slug
            place = photo.PlaceSlug
            filePath = photo.FilePath
            alt = photo.Alt
            credit = photo.Credit
            licence = photo.Licence
            sourceUrl = photo.SourceUrl
            sort = photo.Sort
        |}
    |> ignore

let private upsertCard transaction (card: Card) =
    exec
        transaction
        "insert into cards (slug, place_slug, title, line, photo_slug, illustration_slug, is_start)
         values (@slug, @place, @title, @line, @photo, @illustration, @isStart)
         on conflict (slug) do update set
            place_slug = excluded.place_slug, title = excluded.title, line = excluded.line,
            retired_at = null, photo_slug = excluded.photo_slug, illustration_slug = excluded.illustration_slug, is_start = excluded.is_start"
        {|
            slug = card.Slug
            place = card.PlaceSlug
            title = card.Title
            line = card.Line
            photo = card.PhotoSlug
            illustration = card.IllustrationSlug
            isStart = card.IsStart
        |}
    |> ignore

let private replaceBranches transaction (card: Card) =
    exec transaction "delete from card_branches where from_card = @card" {| card = card.Slug |}
    |> ignore

    exec transaction "delete from card_signals where card_slug = @card" {| card = card.Slug |}
    |> ignore

    for branch in card.Branches do
        exec
            transaction
            "insert into card_branches (from_card, on_answer, to_card, priority)
             values (@card, @answer, @toSlug, @priority)"
            {|
                card = card.Slug
                answer = branch.OnAnswer
                toSlug = branch.ToCard
                priority = branch.Priority
            |}
        |> ignore

    for signal in card.Signals do
        exec transaction "insert into card_signals (card_slug, signal, weight) values (@card, @signal, @weight)" {|
            card = card.Slug
            signal = signal.Signal
            weight = signal.Weight
        |}
        |> ignore

let private retireMissing transaction (catalog: Catalog) =
    exec transaction "update cards set retired_at = now() where retired_at is null and not (slug = any(@slugs))" {|
        slugs = (catalog.Cards |> List.map _.Slug |> List.toArray)
    |}
    |> ignore

let apply transaction (catalog: Catalog) =
    catalog.Illustrations |> List.iter (upsertIllustration transaction)
    catalog.Photos |> List.iter (upsertPhoto transaction)
    catalog.Cards |> List.iter (upsertCard transaction)
    catalog.Cards |> List.iter (replaceBranches transaction)

    if catalog.Complete then
        retireMissing transaction catalog
