module Namastay.Data.Places.Search

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Places.Rows
open Namastay.Shared.Contracts

let private searchSql =
    "with matches as (
        select p.slug, p.name, r.name as region, f.deep as family_deep, p.published,
               greatest(
                   similarity(p.name, @query),
                   case when position(lower(@query) in lower(p.name)) > 0 then 1 else 0 end,
                   case when to_tsvector('simple', p.name || ' ' || p.state || ' ' || r.name || ' ' || p.summary)
                             @@ plainto_tsquery('simple', @query) then 0.5 else 0 end
               ) as score
        from places p
        join clusters c on c.slug = p.cluster_slug
        join regions r on r.slug = c.region_slug
        join colour_families f on f.slug = p.family_slug
        where p.retired_at is null
     )
     select slug, name, region, family_deep, published
     from matches
     where score > 0.25
     order by score desc, name
     limit 8"

let toSummary (row: SummaryRow) = {
    Slug = row.Slug
    Name = row.Name
    Region = row.Region
    FamilyDeep = row.FamilyDeep
    Published = row.Published
}

let run (source: NpgsqlDataSource) (query: string) : Task<PlaceSummary list> =
    task {
        let! rows = Namastay.Data.Sql.query<SummaryRow> source searchSql {| query = query |}

        return rows |> List.map toSummary
    }
