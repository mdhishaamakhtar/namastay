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
        [
            arg "slug" illustration.Slug
            arg "filePath" illustration.FilePath
            arg "alt" illustration.Alt
            arg "brief" illustration.Brief
            arg "status" illustration.Status
        ]
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
        [
            arg "slug" photo.Slug
            arg "place" photo.PlaceSlug
            arg "filePath" photo.FilePath
            arg "alt" photo.Alt
            arg "credit" photo.Credit
            arg "licence" photo.Licence
            arg "sourceUrl" photo.SourceUrl
            arg "sort" photo.Sort
        ]
    |> ignore

let private upsertCard transaction (card: Card) =
    exec
        transaction
        "insert into cards (slug, place_slug, title, line, illustration_slug, is_start)
         values (@slug, @place, @title, @line, @illustration, @isStart)
         on conflict (slug) do update set
            place_slug = excluded.place_slug, title = excluded.title, line = excluded.line,
            illustration_slug = excluded.illustration_slug, is_start = excluded.is_start"
        [
            arg "slug" card.Slug
            opt "place" card.PlaceSlug
            arg "title" card.Title
            arg "line" card.Line
            opt "illustration" card.IllustrationSlug
            arg "isStart" card.IsStart
        ]
    |> ignore

let private replaceBranches transaction (card: Card) =
    exec transaction "delete from card_branches where from_card = @card" [ arg "card" card.Slug ]
    |> ignore

    exec transaction "delete from card_signals where card_slug = @card" [ arg "card" card.Slug ]
    |> ignore

    for branch in card.Branches do
        exec
            transaction
            "insert into card_branches (from_card, on_answer, to_card, priority)
             values (@card, @answer, @to, @priority)"
            [
                arg "card" card.Slug
                arg "answer" branch.OnAnswer
                arg "to" branch.ToCard
                arg "priority" branch.Priority
            ]
        |> ignore

    for signal in card.Signals do
        exec transaction "insert into card_signals (card_slug, signal, weight) values (@card, @signal, @weight)" [
            arg "card" card.Slug
            arg "signal" signal.Signal
            arg "weight" signal.Weight
        ]
        |> ignore

let apply transaction (catalog: Catalog) =
    catalog.Illustrations |> List.iter (upsertIllustration transaction)
    catalog.Photos |> List.iter (upsertPhoto transaction)
    catalog.Cards |> List.iter (upsertCard transaction)
    catalog.Cards |> List.iter (replaceBranches transaction)
