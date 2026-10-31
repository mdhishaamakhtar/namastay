module Namastay.Data.Places.Search

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql
open Namastay.Shared.Contracts

let private searchSql =
    "with matches as (
        select p.slug, p.name, r.name as region, f.deep, p.published,
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
     select slug, name, region, deep, published
     from matches
     where score > 0.25
     order by score desc, name
     limit 8"

let run (source: NpgsqlDataSource) (query: string) : Task<PlaceSummary list> =
    Namastay.Data.Sql.query source searchSql [ arg "query" query ] (fun reader -> {
        Slug = reader.GetString 0
        Name = reader.GetString 1
        Region = reader.GetString 2
        FamilyDeep = reader.GetString 3
        Published = reader.GetBoolean 4
    })
