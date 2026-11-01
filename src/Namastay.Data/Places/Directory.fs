module Namastay.Data.Places.Directory

open System.Threading.Tasks
open Npgsql
open Namastay.Data
open Namastay.Shared
open Namastay.Shared.Contracts

let private mostDishes = 5
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
    Place: string
    PlaceSlug: string
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
    "select r.slug as region_slug, r.name as region, r.colour, d.name as dish, p.name as place, p.slug as place_slug
     from dishes d
     join places p on p.slug = d.place_slug
     join clusters c on c.slug = p.cluster_slug
     join regions r on r.slug = c.region_slug
     where p.published and p.retired_at is null
     order by r.sort nulls last, r.name, p.name, d.name"

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
        |> List.truncate mostDishes
        |> List.map (fun row -> {
            Slug = PlaceName.slug row.Dish
            Name = row.Dish
            Place = PlaceName.plain row.Place
            PlaceSlug = row.PlaceSlug
        })
}

let food (source: NpgsqlDataSource) : Task<FoodPage> =
    task {
        let! landing = Landing.page source
        let! rows = Sql.query<DishRow> source dishesSql null

        return {
            Cards = landing.Cards |> List.filter _.Food
            Regions = rows |> List.groupBy _.RegionSlug |> List.map (snd >> toFoodRegion)
        }
    }
