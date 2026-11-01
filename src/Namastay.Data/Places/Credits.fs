module Namastay.Data.Places.Credits

open System.Threading.Tasks
open Npgsql
open Namastay.Data
open Namastay.Shared
open Namastay.Shared.Contracts

[<CLIMutable>]
type CreditRow = {
    Place: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
}

let private sql =
    "select p.name as place, ph.alt, ph.credit, ph.licence, ph.source_url
     from photos ph
     join places p on p.slug = ph.place_slug
     where p.published and p.retired_at is null
     order by p.name, ph.sort"

let all (source: NpgsqlDataSource) : Task<PhotoCredit list> =
    task {
        let! rows = Sql.query<CreditRow> source sql null

        return
            rows
            |> List.map (fun row -> {
                Place = PlaceName.plain row.Place
                Subject = row.Alt
                Credit = row.Credit
                Licence = row.Licence
                SourceUrl = row.SourceUrl
            })
    }
