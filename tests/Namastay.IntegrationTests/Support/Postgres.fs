namespace Namastay.IntegrationTests

open System
open System.Threading.Tasks
open Npgsql
open Testcontainers.PostgreSql
open Xunit

/// One Postgres container for the whole test run. Each test gets its own database.
type PostgresFixture() =
    let container = PostgreSqlBuilder("postgres:17").Build()

    member _.NewDatabase() =
        task {
            let name = "t" + Guid.NewGuid().ToString("N")
            use admin = new NpgsqlConnection(container.GetConnectionString())
            do! admin.OpenAsync()
            use command = new NpgsqlCommand($"create database {name}", admin)
            let! _ = command.ExecuteNonQueryAsync()
            let builder = NpgsqlConnectionStringBuilder(container.GetConnectionString())
            builder.Database <- name
            return builder.ConnectionString
        }

    interface IAsyncLifetime with
        member _.InitializeAsync() = container.StartAsync()
        member _.DisposeAsync() = container.DisposeAsync().AsTask()

/// Runs only where the private data folder exists, and shows as skipped elsewhere.
type PrivateDataFactAttribute() as this =
    inherit FactAttribute()

    do
        if not (IO.Directory.Exists PrivateDataFactAttribute.Folder) then
            this.Skip <- "The private data folder isn't available."

    static member Folder = IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "..", "data")

[<CollectionDefinition("postgres")>]
type PostgresCollection() =
    interface ICollectionFixture<PostgresFixture>
