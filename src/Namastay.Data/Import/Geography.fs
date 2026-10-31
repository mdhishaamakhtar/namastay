module Namastay.Data.Import.Geography

open Namastay.Data.Sql
open Namastay.Domain.Catalog

let private upsertRegion transaction (region: Region) =
    exec
        transaction
        "insert into regions (slug, name) values (@slug, @name)
         on conflict (slug) do update set name = excluded.name"
        {|
            slug = region.Slug
            name = region.Name
        |}
    |> ignore

let private upsertCluster transaction (cluster: Cluster) =
    exec
        transaction
        "insert into clusters (slug, region_slug, name) values (@slug, @region, @name)
         on conflict (slug) do update set region_slug = excluded.region_slug, name = excluded.name"
        {|
            slug = cluster.Slug
            region = cluster.RegionSlug
            name = cluster.Name
        |}
    |> ignore

let private upsertFamily transaction (family: ColourFamily) =
    exec
        transaction
        "insert into colour_families (slug, name, used_for, deep, mid, pale, text_on_deep)
         values (@slug, @name, @usedFor, @deep, @mid, @pale, @textOnDeep)
         on conflict (slug) do update set
            name = excluded.name, used_for = excluded.used_for, deep = excluded.deep,
            mid = excluded.mid, pale = excluded.pale, text_on_deep = excluded.text_on_deep"
        {|
            slug = family.Slug
            name = family.Name
            usedFor = family.UsedFor
            deep = family.Deep
            mid = family.Mid
            pale = family.Pale
            textOnDeep = family.TextOnDeep
        |}
    |> ignore

let private upsertInterest transaction (interest: Interest) =
    exec
        transaction
        "insert into interests (slug, name) values (@slug, @name)
         on conflict (slug) do update set name = excluded.name"
        {|
            slug = interest.Slug
            name = interest.Name
        |}
    |> ignore

let private linkParent transaction (interest: Interest) =
    exec transaction "update interests set parent_slug = @parent where slug = @slug" {|
        slug = interest.Slug
        parent = interest.ParentSlug
    |}
    |> ignore

let private upsertSource transaction (source: Source) =
    exec
        transaction
        "insert into sources (id, url, title, publisher, kind, accessed_on)
         values (@id, @url, @title, @publisher, @kind, @accessedOn)
         on conflict (id) do update set
            url = excluded.url, title = excluded.title, publisher = excluded.publisher,
            kind = excluded.kind, accessed_on = excluded.accessed_on"
        {|
            id = source.Id
            url = source.Url
            title = source.Title
            publisher = source.Publisher
            kind = Encode.sourceKind source.Kind
            accessedOn = source.AccessedOn
        |}
    |> ignore

let apply transaction (catalog: Catalog) =
    catalog.Regions |> List.iter (upsertRegion transaction)
    catalog.Clusters |> List.iter (upsertCluster transaction)
    catalog.Families |> List.iter (upsertFamily transaction)
    catalog.Interests |> List.iter (upsertInterest transaction)
    catalog.Interests |> List.iter (linkParent transaction)
    catalog.Sources |> List.iter (upsertSource transaction)
