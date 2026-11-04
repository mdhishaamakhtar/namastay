module Namastay.Data.Places.Feelings

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Places.Rows
open Namastay.Domain.Trip
open Namastay.Shared
open Namastay.Shared.Contracts

let private namedPlaces = 3
let private shownPlaces = 12

[<CLIMutable>]
type FeelingRow = {
    Slug: string
    Name: string
    Place: string
    Colour: string
}

let private feelingsSql =
    "select i.slug, i.name, p.name as place, coalesce(i.colour, f.deep) as colour
     from interests i
     join place_interests pi on pi.interest_slug = i.slug
     join places p on p.slug = pi.place_slug
     join colour_families f on f.slug = p.family_slug
     where p.published and p.retired_at is null and pi.weight >= @strongFit and i.parent_slug is null
     order by i.slug, pi.weight desc, p.name"

let private toFeeling (rows: FeelingRow list) : Feeling = {
    Slug = rows.Head.Slug
    Name = rows.Head.Name
    Places = rows |> List.truncate namedPlaces |> List.map (_.Place >> PlaceName.plain)
    Colour =
        rows
        |> List.countBy _.Colour
        |> List.sortBy (fun (colour, uses) -> -uses, colour)
        |> List.head
        |> fst
}

/// Every interest with places to show, the fullest first.
let all (source: NpgsqlDataSource) : Task<Feeling list> =
    task {
        let! rows =
            Namastay.Data.Sql.query<FeelingRow> source feelingsSql {| strongFit = Scope.strongFit |}

        return
            rows
            |> List.groupBy _.Slug
            |> List.sortBy (fun (slug, items) -> -items.Length, slug)
            |> List.map (snd >> toFeeling)
    }

let private placesSql =
    "select p.slug, p.name, r.name as region, f.deep as family_deep, p.published
     from place_interests pi
     join places p on p.slug = pi.place_slug
     join clusters c on c.slug = p.cluster_slug
     join regions r on r.slug = c.region_slug
     join colour_families f on f.slug = p.family_slug
     where pi.interest_slug = @slug and p.published and p.retired_at is null and pi.weight >= @strongFit
     order by pi.weight desc, p.name
     limit @limit"

let places (source: NpgsqlDataSource) (slug: string) : Task<PlaceSummary list> =
    task {
        let! rows =
            Namastay.Data.Sql.query<SummaryRow> source placesSql {|
                slug = slug
                strongFit = Scope.strongFit
                limit = shownPlaces
            |}

        return rows |> List.map Search.toSummary
    }
