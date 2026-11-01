module Namastay.Data.Places.LeadPhoto

open System.Threading.Tasks
open Npgsql
open Namastay.Shared
open Namastay.Shared.Contracts

[<CLIMutable>]
type PhotoRow = {
    FilePath: string
    Alt: string
    Credit: string
    Licence: string
    SourceUrl: string
}

/// One place's lead photo, for pages that aren't about any particular place.
let tryFind (source: NpgsqlDataSource) : Task<GuidePhoto option> =
    task {
        let! rows =
            Namastay.Data.Sql.query<PhotoRow>
                source
                "select ph.file_path, ph.alt, ph.credit, ph.licence, ph.source_url
                 from photos ph
                 join places p on p.slug = ph.place_slug
                 where p.published and p.retired_at is null
                 order by p.slug, ph.sort
                 limit 1"
                null

        return
            rows
            |> List.tryHead
            |> Option.map (fun row -> {
                Url = Routes.mediaUrl row.FilePath
                Alt = row.Alt
                Credit = row.Credit
                Licence = row.Licence
                SourceUrl = row.SourceUrl
            })
    }
