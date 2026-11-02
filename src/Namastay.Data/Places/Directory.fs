module Namastay.Data.Places.Directory

open System.Threading.Tasks
open Npgsql
open Namastay.Data
open Namastay.Shared
open Namastay.Shared.Contracts

let private plainColour = "#3A2C0A"

[<CLIMutable>]
type PlaceRow = {
    RegionSlug: string
    Region: string
    Colour: string option
    Slug: string
    Name: string
    FamilyDeep: string
}

[<CLIMutable>]
type DishRow = {
    RegionSlug: string
    Region: string
    Colour: string option
    Dish: string
    Vegetarian: bool
    Vegan: bool
    Place: string
    PlaceSlug: string
    FilePath: string option
    Alt: string option
    Credit: string option
    Licence: string option
    SourceUrl: string option
}

let private placesSql =
    "select r.slug as region_slug, r.name as region, r.colour, p.slug, p.name, f.deep as family_deep
     from places p
     join clusters c on c.slug = p.cluster_slug
     join regions r on r.slug = c.region_slug
     join colour_families f on f.slug = p.family_slug
     where p.published and p.retired_at is null
     order by r.sort nulls last, r.name, p.name"

let private dishesSql =
    "select r.slug as region_slug, r.name as region, r.colour, d.name as dish, d.vegetarian, d.vegan, p.name as place, p.slug as place_slug,
            ph.file_path, ph.alt, ph.credit, ph.licence, ph.source_url
     from dishes d
     left join photos ph on ph.slug = d.photo_slug
     join places p on p.slug = d.place_slug
     join clusters c on c.slug = p.cluster_slug
     join regions r on r.slug = c.region_slug
     where p.published and p.retired_at is null and d.photo_slug is not null
     order by r.sort nulls last, r.name, d.rank nulls last, p.name, d.name"

let private toRegion (rows: PlaceRow list) = {
    Slug = rows.Head.RegionSlug
    Name = rows.Head.Region
    Colour = rows.Head.Colour |> Option.defaultValue plainColour
    Places =
        rows
        |> List.map (fun row -> {
            Slug = row.Slug
            Name = PlaceName.plain row.Name
            Region = row.Region
            FamilyDeep = row.FamilyDeep
            Published = true
        })
}

let regions (source: NpgsqlDataSource) : Task<DirectoryRegion list> =
    task {
        let! rows = Sql.query<PlaceRow> source placesSql null
        return rows |> List.groupBy _.RegionSlug |> List.map (snd >> toRegion)
    }

let private toFoodRegion (rows: DishRow list) = {
    Slug = rows.Head.RegionSlug
    Name = rows.Head.Region
    Colour = rows.Head.Colour |> Option.defaultValue plainColour
    Dishes =
        rows
        |> List.distinctBy _.Dish
        |> List.map (fun row -> {
            Slug = PlaceName.slug row.Dish
            Name = row.Dish
            Kind = DishKinds.ofFlags row.Vegetarian row.Vegan
            Place = PlaceName.plain row.Place
            PlaceSlug = row.PlaceSlug
        })
}

/// A region's best-known dish that has a photo, to lead the Food page.
let private signature (rows: DishRow list) : LandingCard option =
    rows
    |> List.tryPick (fun row ->
        match row.FilePath, row.Alt, row.Credit, row.Licence, row.SourceUrl with
        | Some path, Some alt, Some credit, Some licence, Some source ->
            Some {
                PlaceSlug = row.PlaceSlug
                Place = row.Region
                Title = row.Dish
                Colour = row.Colour |> Option.defaultValue plainColour
                Food = true
                Photo = {
                    Url = Routes.mediaUrl path
                    Alt = alt
                    Credit = credit
                    Licence = licence
                    SourceUrl = source
                }
            }
        | _ -> None)

let food (source: NpgsqlDataSource) : Task<FoodPage> =
    task {
        let! rows = Sql.query<DishRow> source dishesSql null

        return {
            Cards = rows |> List.groupBy _.RegionSlug |> List.choose (snd >> signature)
            Regions = rows |> List.groupBy _.RegionSlug |> List.map (snd >> toFoodRegion)
        }
    }
