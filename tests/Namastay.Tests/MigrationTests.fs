namespace Namastay.Tests

open Npgsql
open Xunit
open Namastay.Data

[<Collection("postgres")>]
type MigrationTests(postgres: PostgresFixture) =
    let expected =
        [ "regions"; "clusters"; "colour_families"; "places"; "interests"; "place_interests"
          "place_months"; "traveller_fit"; "experiences"; "day_plans"; "stay_areas"
          "transport_links"; "trains"; "festivals"; "festival_dates"; "notices"; "exclusions"
          "sources"; "fact_sources"; "cards"; "card_branches"; "card_signals"; "illustrations"
          "users"; "itineraries"; "import_batches" ]

    [<Fact>]
    member _.migrations_create_all_tables() =
        task {
            let! connectionString = postgres.NewDatabase()
            Assert.True((Migrate.run connectionString = Ok()))
            use connection = new NpgsqlConnection(connectionString)
            do! connection.OpenAsync()
            use command =
                new NpgsqlCommand("select table_name from information_schema.tables where table_schema = 'public'", connection)
            use! reader = command.ExecuteReaderAsync()
            let tables = ResizeArray<string>()
            while reader.Read() do
                tables.Add(reader.GetString 0)
            for table in expected do
                Assert.Contains(table, tables)
        }

    [<Fact>]
    member _.pg_trgm_is_enabled() =
        task {
            let! connectionString = postgres.NewDatabase()
            Assert.True((Migrate.run connectionString = Ok()))
            use connection = new NpgsqlConnection(connectionString)
            do! connection.OpenAsync()
            use command = new NpgsqlCommand("select similarity('rishikesh', 'rishkesh') > 0.3", connection)
            let! result = command.ExecuteScalarAsync()
            Assert.Equal(box true, result)
        }

    [<Fact>]
    member _.migrations_are_repeatable() =
        task {
            let! connectionString = postgres.NewDatabase()
            Assert.True((Migrate.run connectionString = Ok()))
            Assert.True((Migrate.run connectionString = Ok()))
        }
