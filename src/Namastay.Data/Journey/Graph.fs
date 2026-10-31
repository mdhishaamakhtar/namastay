module Namastay.Data.Journey.Graph

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql
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
    Deep: string option
    Mid: string option
    Pale: string option
    TextOnDeep: string option
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
            f.deep, f.mid, f.pale, f.text_on_deep
     from cards c
     left join photos photo on photo.slug = c.photo_slug
     left join illustrations mark on mark.slug = c.illustration_slug
     left join places p on p.slug = c.place_slug
     left join colour_families f on f.slug = p.family_slug
     order by c.slug"

let private mediaUrl (path: string) = "/media/" + path

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
let private toCard (branches: BranchRow list) (row: CardRow) : JourneyCard =
    let hasPhoto = row.PhotoPath.IsSome

    {
        Slug = row.Slug
        Title = row.Title
        Line = row.Line
        IsStart = row.IsStart
        ImageUrl = row.PhotoPath |> Option.orElse row.MarkPath |> Option.map mediaUrl
        ImageAlt = if hasPhoto then row.PhotoAlt else row.MarkAlt
        ImageCredit = row.PhotoCredit
        MarkUrl =
            if hasPhoto then
                row.MarkPath |> Option.map mediaUrl
            else
                None
        Colours = colours row
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
                "select from_card, on_answer, to_card, priority from card_branches order by priority desc, to_card"
                null

        return {
            Cards = cards |> List.map (toCard branches)
        }
    }
