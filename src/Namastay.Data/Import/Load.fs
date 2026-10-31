module Namastay.Data.Import.Load

open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open Namastay.Domain.Catalog

type private Manifest = { Complete: bool }

let private options =
    let fsharp =
        JsonFSharpOptions
            .Default()
            .WithUnionUnwrapFieldlessTags()
            .WithUnionTagNamingPolicy(JsonNamingPolicy.SnakeCaseLower)
            .WithSkippableOptionFields(SkippableOptionFields.Always)

    let settings = JsonSerializerOptions(JsonSerializerDefaults.Web)
    settings.WriteIndented <- true
    settings.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    settings.Converters.Add(JsonFSharpConverter fsharp)
    settings

let serialise (value: 'T) =
    JsonSerializer.Serialize(value, options)

let private fileNames = {|
    Regions = "core/regions.json"
    Clusters = "core/clusters.json"
    Families = "core/colour-families.json"
    Interests = "core/interests.json"
    Places = "core/places.json"
    Links = "core/transport-links.json"
    Festivals = "core/festivals.json"
    Notices = "core/notices.json"
    Exclusions = "core/exclusions.json"
    Sources = "core/sources.json"
    Illustrations = "journey/illustrations.json"
    Photos = "core/photos.json"
    Cards = "journey/cards.json"
|}

/// Writes a catalog in the folder layout that `fromFolder` reads.
let save (folder: string) (catalog: Catalog) =
    let write (name: string) (value: 'T) =
        let path = Path.Combine(folder, name)
        Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
        File.WriteAllText(path, serialise value)

    write "manifest.json" { Complete = catalog.Complete }
    write fileNames.Regions catalog.Regions
    write fileNames.Clusters catalog.Clusters
    write fileNames.Families catalog.Families
    write fileNames.Interests catalog.Interests
    write fileNames.Places catalog.Places
    write fileNames.Links catalog.Links
    write fileNames.Festivals catalog.Festivals
    write fileNames.Notices catalog.Notices
    write fileNames.Exclusions catalog.Exclusions
    write fileNames.Sources catalog.Sources
    write fileNames.Illustrations catalog.Illustrations
    write fileNames.Photos catalog.Photos
    write fileNames.Cards catalog.Cards

let private withOverrides (core: Place list) (full: Place list) =
    let replaced =
        core
        |> List.map (fun place ->
            full
            |> List.tryFind (fun candidate -> candidate.Slug = place.Slug)
            |> Option.defaultValue place)

    let added =
        full
        |> List.filter (fun place -> not (core |> List.exists (fun other -> other.Slug = place.Slug)))

    replaced @ added

/// Reads every file in the folder and reports all problems together. A file that
/// doesn't exist counts as empty, so a batch can carry only what it changes.
let fromFolder (folder: string) : Result<Catalog, string list> =
    if not (Directory.Exists folder) then
        Error [ $"data folder not found: {folder}" ]
    else
        let errors = ResizeArray<string>()

        let parse (path: string) (fallback: 'T) : 'T =
            try
                JsonSerializer.Deserialize<'T>(File.ReadAllText path, options)
            with :? JsonException as error ->
                errors.Add $"{path}: {error.Message}"
                fallback

        let read (name: string) : 'T list =
            let path = Path.Combine(folder, name)
            if File.Exists path then parse path [] else []

        let manifestPath = Path.Combine(folder, "manifest.json")

        let manifest =
            if File.Exists manifestPath then
                parse manifestPath { Complete = false }
            else
                { Complete = false }

        let placesFolder = Path.Combine(folder, "places")

        let full: Place list =
            if Directory.Exists placesFolder then
                Directory.GetFiles(placesFolder, "*.json")
                |> Array.sort
                |> Array.toList
                |> List.choose (fun path -> parse path None)
            else
                []

        let catalog = {
            Complete = manifest.Complete
            Regions = read fileNames.Regions
            Clusters = read fileNames.Clusters
            Families = read fileNames.Families
            Interests = read fileNames.Interests
            Places = withOverrides (read fileNames.Places) full
            Links = read fileNames.Links
            Festivals = read fileNames.Festivals
            Notices = read fileNames.Notices
            Exclusions = read fileNames.Exclusions
            Sources = read fileNames.Sources
            Illustrations = read fileNames.Illustrations
            Photos = read fileNames.Photos
            Cards = read fileNames.Cards
        }

        if errors.Count = 0 then
            Ok catalog
        else
            Error(List.ofSeq errors)
