namespace Namastay.IntegrationTests

open System
open System.Threading.Tasks
open Npgsql
open DotNet.Testcontainers.Builders
open Namastay.Data.Media
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

/// One RustFS container, the same S3 store that local development uses.
type MediaFixture() =
    let container =
        ContainerBuilder("rustfs/rustfs:1.0.4")
            .WithEnvironment("RUSTFS_ACCESS_KEY", "namastay")
            .WithEnvironment("RUSTFS_SECRET_KEY", "namastay-local")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(9000))
            .Build()

    member _.Settings(bucket: string) : MediaSettings =
        let port: uint16 = container.GetMappedPublicPort 9000

        {
            Endpoint = $"http://{container.Hostname}:{port}"
            Bucket = bucket
            AccessKey = "namastay"
            SecretKey = "namastay-local"
        }

    interface IAsyncLifetime with
        member _.InitializeAsync() = container.StartAsync()
        member _.DisposeAsync() = container.DisposeAsync().AsTask()

[<CollectionDefinition("media")>]
type MediaCollection() =
    interface ICollectionFixture<MediaFixture>

[<CollectionDefinition("postgres")>]
type PostgresCollection() =
    interface ICollectionFixture<PostgresFixture>
