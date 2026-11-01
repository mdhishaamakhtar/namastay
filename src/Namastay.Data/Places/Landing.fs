module Namastay.Data.Places.Landing

open System.Threading.Tasks
open Npgsql
open Namastay.Shared
open Namastay.Shared.Contracts

[<CLIMutable>]
type LandingRow = {
    PlaceSlug: string
    Place: string
    Title: string
    Colour: string
    Food: bool
    FilePath: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
}

let private sql =
    "select p.slug as place_slug, p.name as place, c.title, f.deep as colour,
            exists (select 1 from card_signals s
                    where s.card_slug = c.slug and s.signal = 'interest:food' and s.weight >= 3) as food,
            ph.file_path, ph.alt, ph.credit, ph.licence, ph.source_url
     from cards c
     join places p on p.slug = c.place_slug
     join colour_families f on f.slug = p.family_slug
     join photos ph on ph.slug = c.photo_slug
     where c.landing_sort is not null and c.retired_at is null and p.published and p.retired_at is null
     order by c.landing_sort"

let private toCard (row: LandingRow) = {
    PlaceSlug = row.PlaceSlug
    Place = row.Place.Split(" with ")[0]
    Title = row.Title
    Colour = row.Colour
    Food = row.Food
    Photo = {
        Url = Routes.mediaUrl row.FilePath
        Alt = row.Alt
        Credit = row.Credit
        Licence = row.Licence
        SourceUrl = row.SourceUrl
    }
}

let all (source: NpgsqlDataSource) : Task<LandingCard list> =
    task {
        let! rows = Namastay.Data.Sql.query<LandingRow> source sql null
        return rows |> List.map toCard
    }
