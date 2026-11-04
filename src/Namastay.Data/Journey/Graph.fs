module Namastay.Data.Journey.Graph

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql
open Namastay.Shared
open Namastay.Shared.Contracts

[<CLIMutable>]
type private CardRow = {
    Slug: string
    Title: string
    Line: string
    IsStart: bool
    PhotoPath: string option
    PhotoAlt: string option
    PhotoCredit: string option
    MarkPath: string option
    MarkAlt: string option
    Interest: string option
    PlaceSlug: string option
    Region: string option
    Deep: string option
    Mid: string option
    Pale: string option
    TextOnDeep: string option
}

[<CLIMutable>]
type private SignalRow = {
    CardSlug: string
    Slug: string
    Weight: int
}

[<CLIMutable>]
type private InterestRow = {
    Slug: string
    Name: string
    ParentSlug: string option
}

[<CLIMutable>]
type private BranchRow = {
    FromCard: string
    OnAnswer: bool
    ToCard: string
    Priority: int
}

let private cardsSql =
    "select c.slug, c.title, c.line, c.is_start,
            photo.file_path as photo_path, photo.alt as photo_alt, photo.credit as photo_credit,
            mark.file_path as mark_path, mark.alt as mark_alt,
            (select i.name from card_signals signal
             join interests i on signal.signal = 'interest:' || i.slug
             where signal.card_slug = c.slug
             order by signal.weight desc, i.name limit 1) as interest,
            c.place_slug, region.slug as region, f.deep, f.mid, f.pale, f.text_on_deep
     from cards c
     left join photos photo on photo.slug = c.photo_slug
     left join illustrations mark on mark.slug = c.illustration_slug
     left join places p on p.slug = c.place_slug
     left join clusters cluster on cluster.slug = p.cluster_slug
     left join regions region on region.slug = cluster.region_slug
     left join colour_families f on f.slug = p.family_slug
     where c.retired_at is null
     order by c.slug"

let private colours (row: CardRow) =
    match row.Deep, row.Mid, row.Pale, row.TextOnDeep with
    | Some deep, Some mid, Some pale, Some text ->
        Some {
            Deep = deep
            Mid = mid
            Pale = pale
            TextOnDeep = text
        }
    | _ -> None

/// A photo fills the card and the illustration becomes its small mark.
/// Without a photo, the illustration fills the card.
let private toCard (branches: BranchRow list) (signals: SignalRow list) (row: CardRow) : JourneyCard =
    let hasPhoto = row.PhotoPath.IsSome

    {
        Slug = row.Slug
        Title = row.Title
        Line = row.Line
        IsStart = row.IsStart
        ImageUrl = row.PhotoPath |> Option.orElse row.MarkPath |> Option.map Routes.mediaUrl
        ImageAlt = if hasPhoto then row.PhotoAlt else row.MarkAlt
        ImageCredit = row.PhotoCredit
        MarkUrl =
            if hasPhoto then
                row.MarkPath |> Option.map Routes.mediaUrl
            else
                None
        Interest = row.Interest
        Interests =
            signals
            |> List.filter (fun signal -> signal.CardSlug = row.Slug)
            |> List.map (fun signal -> {
                Slug = signal.Slug
                Weight = signal.Weight
            })
        Colours = colours row
        Place = row.PlaceSlug
        Region = row.Region
        Branches =
            branches
            |> List.filter (fun branch -> branch.FromCard = row.Slug)
            |> List.map (fun branch -> {
                OnAnswer = branch.OnAnswer
                ToCard = branch.ToCard
                Priority = branch.Priority
            })
    }

let load (source: NpgsqlDataSource) : Task<JourneyGraph> =
    task {
        let! cards = query<CardRow> source cardsSql null

        let! branches =
            query<BranchRow>
                source
                "select b.from_card, b.on_answer, b.to_card, b.priority from card_branches b
                 join cards target on target.slug = b.to_card and target.retired_at is null
                 order by priority desc, to_card"
                null

        let! signals =
            query<SignalRow>
                source
                "select signal.card_slug, i.slug, signal.weight from card_signals signal
                 join interests i on signal.signal = 'interest:' || i.slug
                 order by signal.weight desc, i.name"
                null

        let! interests =
            query<InterestRow> source "select slug, name, parent_slug from interests order by slug" null

        return {
            Cards = cards |> List.map (toCard branches signals)
            Interests =
                interests
                |> List.map (fun interest -> {
                    Slug = interest.Slug
                    Name = interest.Name
                    Parent = interest.ParentSlug
                })
        }
    }
