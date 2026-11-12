namespace Namastay.IntegrationTests.Data

open System
open Npgsql
open Xunit
open Namastay.Data.Import
open Namastay.Data.Import.ContentTables
open Namastay.Data.Import.Publish
open Namastay.Domain.Catalog
open Namastay.IntegrationTests
open Namastay.IntegrationTests.Support

/// Publishing copies the content of one database, standing in for the local one, into
/// another, standing in for production.
type PublishTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let database () =
        task {
            let! connectionString = scope.NewDatabase()
            migrated connectionString
            return connectionString
        }

    let expectOk result =
        match result with
        | Ok value -> value
        | Error errors -> raise (Xunit.Sdk.XunitException $"expected success, got {errors}")

    let import (db: string) (catalog: Catalog) =
        Run.run db Run.Apply "sample" catalog |> expectOk |> ignore

    /// A local database holding the sample, and a production database that's empty.
    let both () =
        task {
            let! local = database ()
            let! production = database ()
            import local Sample.catalog
            return local, production
        }

    /// The same, after a first publish, so the two match.
    let published () =
        task {
            let! local, production = both ()
            Publish.run local production Run.Apply |> expectOk |> ignore
            return local, production
        }

    let rows (db: string) (query: string) =
        task {
            use connection = new NpgsqlConnection(db)
            do! connection.OpenAsync()
            use command = new NpgsqlCommand(query, connection)
            use! reader = command.ExecuteReaderAsync()
            let found = ResizeArray<string>()

            while reader.Read() do
                found.Add(
                    String.Join(
                        "|",
                        [
                            for index in 0 .. reader.FieldCount - 1 ->
                                if reader.IsDBNull index then
                                    ""
                                else
                                    string (reader.GetValue index)
                        ]
                    )
                )

            return List.ofSeq found
        }

    let count (db: string) (table: string) =
        task {
            let! found = rows db $"select count(*) from {table}"
            return int found.Head
        }

    let changeTo (table: string) (changes: Publish.TableChange list) =
        changes |> List.tryFind (fun change -> change.Table = table)

    [<Fact>]
    member _.``the first publish copies every content table``() =
        task {
            let! local, production = both ()
            let changes = Publish.run local production Run.Apply |> expectOk

            Assert.Equal<string list option>(
                Some [ "kasol"; "manali" ],
                changeTo "places" changes |> Option.map (_.Added >> List.sort)
            )

            for table in ContentTables.all do
                let columns = String.Join(", ", table.Key @ table.Data)

                let query =
                    $"select {columns} from {table.Name} order by {String.Join(',', table.Key)}"

                let! here = rows local query
                let! there = rows production query
                Assert.Equal<string list>(here, there)
        }

    [<Fact>]
    member _.``publishing again changes nothing and records no batch``() =
        task {
            let! local, production = published ()
            let! before = count production "import_batches"
            Assert.Empty(Publish.run local production Run.Apply |> expectOk)
            let! after = count production "import_batches"
            Assert.Equal(1, before)
            Assert.Equal(before, after)
        }

    [<Fact>]
    member _.``one changed row is the only row written``() =
        task {
            let! local, production = published ()
            do! sql local "update places set summary = 'Quieter now.' where slug = 'kasol'"

            do!
                sql
                    production
                    "create table writes (place text); create function note_write() returns trigger as $$ begin insert into writes values (new.slug); return new; end $$ language plpgsql; create trigger note after insert or update on places for each row execute function note_write()"

            let changes = Publish.run local production Run.Apply |> expectOk

            Assert.Equal<Publish.TableChange list>(
                [
                    {
                        Table = "places"
                        Added = []
                        Changed = [ "kasol" ]
                        Removed = []
                    }
                ],
                changes
            )

            let! written = rows production "select place from writes"
            let! summary = rows production "select summary from places where slug = 'kasol'"
            let! batches = count production "import_batches"
            Assert.Equal<string list>([ "kasol" ], written)
            Assert.Equal<string list>([ "Quieter now." ], summary)
            Assert.Equal(2, batches)
        }

    [<Fact>]
    member _.``a dry run says what would change and writes nothing``() =
        task {
            let! local, production = published ()
            do! sql local "update places set summary = 'Quieter now.' where slug = 'kasol'"
            let changes = Publish.run local production Run.DryRun |> expectOk
            Assert.Equal<string list option>(Some [ "kasol" ], changeTo "places" changes |> Option.map _.Changed)
            let! summary = rows production "select summary from places where slug = 'kasol'"
            let! batches = count production "import_batches"
            Assert.NotEqual<string list>([ "Quieter now." ], summary)
            Assert.Equal(1, batches)
        }

    [<Fact>]
    member _.``accounts, shared itineraries and saved trips are left alone``() =
        task {
            let! local, production = both ()

            do!
                sql
                    production
                    "insert into users (id, email, provider) values ('00000000-0000-0000-0000-000000000001', 'visitor@example.org', 'google');
                     insert into itineraries (share_token, title, trip, data_version) values ('abcdefghijkl', 'A trip', '{}', 1);
                     insert into saved_trips (user_id, share_token, title, trip) values ('00000000-0000-0000-0000-000000000001', 'abcdefghijkl', 'Mine', '{}')"

            let people =
                "select email from users union all select title from itineraries union all select title from saved_trips"

            let! before = rows production people
            Publish.run local production Run.Apply |> expectOk |> ignore
            do! sql local "delete from fact_sources; update places set summary = 'Changed.'"
            Publish.run local production Run.Apply |> expectOk |> ignore
            let! after = rows production people
            Assert.Equal<string list>([ "visitor@example.org"; "A trip"; "Mine" ], before)
            Assert.Equal<string list>(before, after)
        }

    [<Fact>]
    member _.``a place gone from the local database is retired, with what belongs to it``() =
        task {
            let! local, production = published ()
            let! rebuilt = database ()

            import rebuilt {
                Sample.catalog with
                    Places = [ Sample.place ]
                    Links = []
            }

            let! monthsBefore = count production "place_months where place_slug = 'manali'"
            let changes = Publish.run rebuilt production Run.Apply |> expectOk
            let! retired = rows production "select slug from places where retired_at is not null"
            let! monthsAfter = count production "place_months where place_slug = 'manali'"
            Assert.Equal<string list>([ "manali" ], retired)
            Assert.Equal<string list option>(Some [ "manali" ], changeTo "places" changes |> Option.map _.Removed)
            Assert.True(monthsBefore > 0)
            Assert.Equal(monthsBefore, monthsAfter)
            Assert.Empty(Publish.run rebuilt production Run.Apply |> expectOk)
            ignore local
        }

    [<Fact>]
    member _.``a retired place that comes back is live again``() =
        task {
            let! local, production = published ()
            do! sql production "update places set retired_at = now() where slug = 'manali'"
            let changes = Publish.run local production Run.Apply |> expectOk
            let! retired = count production "places where retired_at is not null"
            Assert.Equal<string list option>(Some [ "manali" ], changeTo "places" changes |> Option.map _.Changed)
            Assert.Equal(0, retired)
        }

    [<Fact>]
    member _.``a place retired on both sides keeps the day production retired it``() =
        task {
            let! local, production = published ()
            do! sql production "update places set retired_at = '2026-01-01' where slug = 'manali'"
            do! sql local "update places set retired_at = '2026-06-01' where slug = 'manali'"
            Assert.Empty(Publish.run local production Run.Apply |> expectOk)
        }

    [<Fact>]
    member _.``a row removed from a place that's still published is deleted``() =
        task {
            let! local, production = published ()

            let! months =
                rows local "select month from place_months where place_slug = 'kasol' order by month"

            let month = months.Head
            do! sql local $"delete from place_months where place_slug = 'kasol' and month = {month}"
            let changes = Publish.run local production Run.Apply |> expectOk

            let! left =
                rows production "select month from place_months where place_slug = 'kasol' order by month"

            Assert.Equal<string list option>(
                Some [ $"kasol:{month}" ],
                changeTo "place_months" changes |> Option.map _.Removed
            )

            Assert.Equal<string list>(months.Tail, left)
        }

    [<Fact>]
    member _.``a publish production rejects changes nothing``() =
        task {
            let! local, production = both ()
            do! sql production "alter table places add constraint no_manali check (slug <> 'manali')"

            match Publish.run local production Run.Apply with
            | Ok changes -> failwith $"expected the publish to be rejected, got {changes}"
            | Error errors -> Assert.Contains("rejected the publish", errors.Head)

            let! regions = count production "regions"
            let! batches = count production "import_batches"
            Assert.Equal(0, regions)
            Assert.Equal(0, batches)
        }

    [<Fact>]
    member _.``an empty local database is refused before anything is retired``() =
        task {
            let! local, production = published ()
            let! empty = database ()

            match Publish.run empty production Run.Apply with
            | Ok changes -> failwith $"expected a refusal, got {changes}"
            | Error errors -> Assert.Contains("no places", errors.Head)

            let! retired = count production "places where retired_at is not null"
            Assert.Equal(0, retired)
            ignore local
        }

    [<Fact>]
    member _.``a database that can't be reached is a clear error``() =
        task {
            let! local, _ = both ()

            match Publish.run local "Host=127.0.0.1;Port=1;Database=none;Username=x;Password=x" Run.Apply with
            | Ok changes -> failwith $"expected an error, got {changes}"
            | Error errors -> Assert.Contains("couldn't reach the database", errors.Head)
        }

    /// A column added by a migration must be listed, or publishing would quietly leave it behind.
    [<Fact>]
    member _.``the content tables list every column of every table that isn't a person's``() =
        task {
            let! db = database ()

            let! tables =
                rows db "select table_name from information_schema.tables where table_schema = 'public' order by 1"

            let listed = (ContentTables.all |> List.map _.Name) @ ContentTables.notContent
            Assert.Equal<string list>(tables, List.sort listed)

            for table in ContentTables.all do
                let! columns =
                    rows
                        db
                        $"select column_name from information_schema.columns where table_schema = 'public' and table_name = '{table.Name}' order by 1"

                let retired =
                    if table.Removal = Removal.Retire then
                        [ "retired_at" ]
                    else
                        []

                Assert.Equal<string list>(columns, List.sort (table.Key @ table.Data @ retired))
        }

    [<Fact>]
    member _.``the API learns of a publish through its batch``() =
        task {
            let! local, production = both ()
            Publish.run local production Run.Apply |> expectOk |> ignore
            let! batch = rows production "select file_hash, status from import_batches"
            Assert.Equal<string list>([ "sample|committed" ], batch)
        }

    interface IDisposable with
        member _.Dispose() = (scope :> IDisposable).Dispose()

    interface IClassFixture<PostgresFixture>
