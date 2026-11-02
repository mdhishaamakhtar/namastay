/// The pages you click into from Places, Food and the feelings, and what each can plan.
module Namastay.Data.Places.Browse

open System.Threading.Tasks
open Npgsql
open Namastay.Data
open Namastay.Shared
open Namastay.Shared.Contracts

let private plainColour = "#3A2C0A"

/// How strongly a place must suit a feeling to be listed under it.
let private strongFit = 3

[<CLIMutable>]
type private HeadRow = {
    Slug: string
    Name: string
    Colour: string option
}

[<CLIMutable>]
type private ItemRow = {
    Slug: string
    Name: string
    Line: string
    Colour: string
    Vegetarian: bool
    Vegan: bool
}

[<CLIMutable>]
type private DishRow = {
    PlaceSlug: string
    Place: string
    Name: string
    Description: string
    Vegetarian: bool
    Vegan: bool
    Colour: string
    FilePath: string option
    Alt: string option
    Credit: string option
    Licence: string option
    SourceUrl: string option
}

let private toListing (row: ItemRow) : Listing = {
    Slug = row.Slug
    Name = PlaceName.plain row.Name
    Line = row.Line
    Colour = row.Colour
    Kind = None
}

let private pageFrom (head: HeadRow option) (items: Listing list) =
    head
    |> Option.map (fun found -> {
        Slug = found.Slug
        Name = found.Name
        Colour = found.Colour |> Option.defaultValue plainColour
        Items = items
    })

let private regionHead source (slug: string) =
    task {
        let! rows =
            Sql.query<HeadRow> source "select slug, name, colour from regions where slug = @slug" {| slug = slug |}

        return List.tryHead rows
    }

let region (source: NpgsqlDataSource) (slug: string) : Task<BrowsePage option> =
    task {
        let! head = regionHead source slug

        let! rows =
            Sql.query<ItemRow>
                source
                "select p.slug, p.name, p.summary as line, f.deep as colour
                 from places p
                 join clusters c on c.slug = p.cluster_slug
                 join colour_families f on f.slug = p.family_slug
                 where c.region_slug = @slug and p.published and p.retired_at is null
                 order by p.name"
                {| slug = slug |}

        return pageFrom head (rows |> List.map toListing)
    }

let feeling (source: NpgsqlDataSource) (slug: string) : Task<BrowsePage option> =
    task {
        let! heads =
            Sql.query<HeadRow> source "select slug, name, colour from interests where slug = @slug" {| slug = slug |}

        let! rows =
            Sql.query<ItemRow>
                source
                "select p.slug, p.name, p.summary as line, f.deep as colour
                 from place_interests pi
                 join places p on p.slug = pi.place_slug
                 join colour_families f on f.slug = p.family_slug
                 where pi.interest_slug = @slug and pi.weight >= @strongFit and p.published and p.retired_at is null
                 order by pi.weight desc, p.name"
                {| slug = slug; strongFit = strongFit |}

        return pageFrom (List.tryHead heads) (rows |> List.map toListing)
    }

let foodRegion (source: NpgsqlDataSource) (slug: string) : Task<BrowsePage option> =
    task {
        let! head = regionHead source slug

        let! rows =
            Sql.query<ItemRow>
                source
                "select d.name as slug, d.name, d.description as line, f.deep as colour, d.vegetarian, d.vegan
                 from dishes d
                 join places p on p.slug = d.place_slug
                 join clusters c on c.slug = p.cluster_slug
                 join colour_families f on f.slug = p.family_slug
                 where c.region_slug = @slug and p.published and p.retired_at is null and d.photo_slug is not null
                 order by d.rank nulls last, p.name, d.name"
                {| slug = slug |}

        let dishes =
            rows
            |> List.distinctBy _.Name
            |> List.map (fun row -> {
                toListing row with
                    Slug = PlaceName.slug row.Name
                    Kind = Some(DishKinds.ofFlags row.Vegetarian row.Vegan)
            })

        return pageFrom head dishes
    }

let private dishRows (source: NpgsqlDataSource) =
    Sql.query<DishRow>
        source
        "select p.slug as place_slug, p.name as place, d.name, d.description, d.vegetarian, d.vegan, f.deep as colour,
                ph.file_path, ph.alt, ph.credit, ph.licence, ph.source_url
         from dishes d
         join places p on p.slug = d.place_slug
         join colour_families f on f.slug = p.family_slug
         left join photos ph on ph.slug = d.photo_slug
         where p.published and p.retired_at is null and d.photo_slug is not null
         order by p.name"
        null

let private toPhoto (row: DishRow) =
    match row.FilePath, row.Alt, row.Credit, row.Licence, row.SourceUrl with
    | Some path, Some alt, Some credit, Some licence, Some source ->
        Some {
            Url = Routes.mediaUrl path
            Alt = alt
            Credit = credit
            Licence = licence
            SourceUrl = source
        }
    | _ -> None

let dish (source: NpgsqlDataSource) (slug: string) : Task<DishPage option> =
    task {
        let! rows = dishRows source
        let found = rows |> List.filter (fun row -> PlaceName.slug row.Name = slug)

        return
            found
            |> List.tryHead
            |> Option.map (fun first -> {
                Slug = slug
                Name = first.Name
                Description = first.Description
                Kind = DishKinds.ofFlags first.Vegetarian first.Vegan
                Photo = found |> List.tryPick toPhoto
                Places =
                    found
                    |> List.map (fun row ->
                        ({
                            Slug = row.PlaceSlug
                            Name = PlaceName.plain row.Place
                            Line = row.Description
                            Colour = row.Colour
                            Kind = None
                        }
                        : Listing))
            })
    }

/// The places a trip may use when it's limited to this scope.
let placesIn (source: NpgsqlDataSource) (scope: TripScope) : Task<Set<string>> =
    task {
        match scope.Kind with
        | ScopeKind.Region
        | ScopeKind.FoodInRegion ->
            let! page = region source scope.Slug

            return
                page
                |> Option.map (_.Items >> List.map _.Slug >> Set.ofList)
                |> Option.defaultValue Set.empty
        | ScopeKind.Feeling ->
            let! page = feeling source scope.Slug

            return
                page
                |> Option.map (_.Items >> List.map _.Slug >> Set.ofList)
                |> Option.defaultValue Set.empty
        | ScopeKind.Dish ->
            let! page = dish source scope.Slug

            return
                page
                |> Option.map (_.Places >> List.map _.Slug >> Set.ofList)
                |> Option.defaultValue Set.empty
    }
