namespace Namastay.IntegrationTests.Data

open System
open System.IO
open Npgsql
open Xunit
open Namastay.Data
open Namastay.Data.Import
open Namastay.Domain.Catalog
open Namastay.IntegrationTests
open Namastay.IntegrationTests.Support

[<Collection("postgres")>]
type ImporterTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let database () =
        task {
            let! connectionString = scope.NewDatabase()
            migrated connectionString
            return connectionString
        }

    let scalar (connectionString: string) (sql: string) =
        task {
            use connection = new NpgsqlConnection(connectionString)
            do! connection.OpenAsync()
            use command = new NpgsqlCommand(sql, connection)
            let! value = command.ExecuteScalarAsync()
            return value
        }

    let count connectionString table =
        task {
            let! value = scalar connectionString $"select count(*) from {table}"
            return Convert.ToInt32 value
        }

    let expectOk result =
        match result with
        | Ok value -> value
        | Error errors -> raise (Xunit.Sdk.XunitException $"expected success, got {errors}")

    let expectErrors result =
        match result with
        | Ok _ -> raise (Xunit.Sdk.XunitException "expected an error")
        | Error errors -> errors

    [<Fact>]
    member _.``unreachable database is a clear error``() =
        let errors =
            Run.run "Host=127.0.0.1;Port=1;Database=none;Username=x;Password=x" Run.Apply "x" Sample.catalog
            |> expectErrors

        Assert.Contains("couldn't reach the database", errors.Head)

    [<Fact>]
    member _.``dry run writes nothing``() =
        task {
            let! db = database ()
            let counts = Run.run db Run.DryRun "hash" Sample.catalog |> expectOk
            Assert.Equal(2, counts.["places"])
            let! places = count db "places"
            Assert.Equal(0, places)
        }

    [<Fact>]
    member _.``invalid batch changes nothing``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore

            let broken = {
                Sample.catalog with
                    Places = [
                        {
                            Sample.place with
                                ClusterSlug = "nowhere"
                                Name = "Changed"
                        }
                    ]
            }

            let errors = Run.run db Run.Apply "two" broken |> expectErrors
            Assert.Contains(errors, (fun error -> error.Contains "nowhere"))

            let! name = scalar db "select name from places where slug = 'kasol'"
            Assert.Equal(box "Kasol", name)
        }

    [<Fact>]
    member _.``second import updates by slug``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore

            let changed = {
                Sample.catalog with
                    Places = [
                        {
                            Sample.place with
                                Name = "Kasol town"
                                Months = [
                                    {
                                        Sample.place.Months.Head with
                                            Month = 6
                                    }
                                ]
                        }
                        {
                            Sample.place with
                                Slug = "manali"
                                Name = "Manali"
                                Experiences = []
                        }
                    ]
            }

            Run.run db Run.Apply "two" changed |> expectOk |> ignore
            let! places = count db "places"
            let! name = scalar db "select name from places where slug = 'kasol'"
            let! month = scalar db "select month from place_months where place_slug = 'kasol'"
            Assert.Equal(2, places)
            Assert.Equal(box "Kasol town", name)
            Assert.Equal(box 6, month)
        }

    [<Fact>]
    member _.``removed place is retired not deleted``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore

            let smaller = {
                Sample.catalog with
                    Complete = true
                    Places = [ Sample.place ]
                    Links = []
            }

            Run.run db Run.Apply "two" smaller |> expectOk |> ignore
            let! places = count db "places"

            let! retired =
                scalar db "select retired_at is not null from places where slug = 'manali'"

            Assert.Equal(2, places)
            Assert.Equal(box true, retired)
        }

    [<Fact>]
    member _.``partial batch retires nothing``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore

            let partial = {
                Sample.catalog with
                    Places = [ Sample.place ]
                    Links = []
            }

            Run.run db Run.Apply "two" partial |> expectOk |> ignore
            let! retired = scalar db "select count(*) from places where retired_at is not null"
            Assert.Equal(0, Convert.ToInt32 retired)
        }

    [<Fact>]
    member _.``returning place is no longer retired``() =
        task {
            let! db = database ()

            let smaller = {
                Sample.catalog with
                    Complete = true
                    Places = [ Sample.place ]
                    Links = []
            }

            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore
            Run.run db Run.Apply "two" smaller |> expectOk |> ignore

            Run.run db Run.Apply "three" { Sample.catalog with Complete = true }
            |> expectOk
            |> ignore

            let! retired =
                scalar db "select retired_at is null from places where slug = 'manali'"

            Assert.Equal(box true, retired)
        }

    [<Fact>]
    member _.``removed link is retired not deleted``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore

            Run.run db Run.Apply "two" {
                Sample.catalog with
                    Complete = true
                    Links = []
            }
            |> expectOk
            |> ignore

            let! retired =
                scalar db "select retired_at is not null from transport_links where slug = 'kasol-manali'"

            Assert.Equal(box true, retired)
        }

    [<Fact>]
    member _.``removed experience is retired not deleted``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore

            let without = {
                Sample.catalog with
                    Places = Sample.catalog.Places |> List.map (fun place -> { place with Experiences = [] })
            }

            Run.run db Run.Apply "two" without |> expectOk |> ignore

            let! retired =
                scalar db "select retired_at is not null from experiences where slug = 'kheerganga'"

            Assert.Equal(box true, retired)
        }

    [<Fact>]
    member _.``public sample data imports cleanly``() =
        task {
            let! db = database ()
            let folder = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "..", "samples", "data")
            let catalog = Load.fromFolder folder |> expectOk
            Run.run db Run.Apply "sample" catalog |> expectOk |> ignore
            let! published = count db "places where published"
            let! photos = count db "photos"
            Assert.Equal(2, published)
            Assert.Equal(1, photos)
        }

    [<PrivateDataFact>]
    member _.``core data imports with the expected counts``() =
        task {
            let! db = database ()
            let catalog = Load.fromFolder PrivateDataFactAttribute.Folder |> expectOk
            Run.run db Run.Apply "data" catalog |> expectOk |> ignore
            let! families = count db "colour_families"
            let! clusters = count db "clusters"
            let! places = count db "places"
            let! exclusions = count db "exclusions"
            let! published = count db "places where published"
            Assert.Equal(16, families)
            Assert.Equal(23, clusters)
            Assert.Equal(60, places)
            Assert.Equal(26, exclusions)
            Assert.Equal(59, published)
        }

    [<Fact>]
    member _.``batch is recorded``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "abc123" Sample.catalog |> expectOk |> ignore

            let! status =
                scalar db "select status from import_batches where file_hash = 'abc123'"

            let! places =
                scalar db "select (counts ->> 'places')::int from import_batches where file_hash = 'abc123'"

            Assert.Equal(box "committed", status)
            Assert.Equal(box 2, places)
        }

    [<Fact>]
    member _.``failed batch is recorded as failed``() =
        task {
            let! db = database ()

            let broken = {
                Sample.catalog with
                    Links = [ { Sample.link with ToSlug = "nowhere" } ]
            }

            Run.run db Run.Apply "bad" broken |> ignore
            let! status = scalar db "select status from import_batches where file_hash = 'bad'"
            Assert.Equal(box "failed", status)
        }

    [<Fact>]
    member _.``evidence sources are stored per row``() =
        task {
            let! db = database ()
            Run.run db Run.Apply "one" Sample.catalog |> expectOk |> ignore

            let! rows =
                count db "fact_sources where table_name = 'places' and row_slug = 'kasol'"

            Assert.Equal(1, rows)
        }

    [<Fact>]
    member _.``catalog round trips through a data folder``() =
        let folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N")

        try
            Load.save folder Sample.catalog

            let loaded = Load.fromFolder folder |> expectOk
            Assert.Equal(Sample.catalog, loaded)
        finally
            Directory.Delete(folder, true)

    [<Fact>]
    member _.``place file overrides the core record``() =
        let folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N")

        try
            Load.save folder Sample.catalog
            Directory.CreateDirectory(Path.Combine(folder, "places")) |> ignore

            let full = {
                Sample.place with
                    Summary = "The full guide."
            }

            File.WriteAllText(Path.Combine(folder, "places", "kasol.json"), Load.serialise full)

            let loaded = Load.fromFolder folder |> expectOk
            let kasol = loaded.Places |> List.find (fun place -> place.Slug = "kasol")
            Assert.Equal("The full guide.", kasol.Summary)
            Assert.Equal(2, loaded.Places.Length)
        finally
            Directory.Delete(folder, true)

    [<Fact>]
    member _.``malformed json names the file``() =
        let folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N")

        try
            Load.save folder Sample.catalog
            File.WriteAllText(Path.Combine(folder, "core", "regions.json"), "{ not json")

            let errors = Load.fromFolder folder |> expectErrors
            Assert.Contains(errors, (fun error -> error.Contains "regions.json"))
        finally
            Directory.Delete(folder, true)

    [<Fact>]
    member _.``missing folder is an error``() =
        let errors = Load.fromFolder "/no/such/folder" |> expectErrors
        Assert.Contains(errors, (fun error -> error.Contains "/no/such/folder"))

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()
