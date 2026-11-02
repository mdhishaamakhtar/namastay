namespace Namastay.IntegrationTests.Http

open System
open System.Threading
open System.Threading.Tasks
open Npgsql
open Xunit
open Namastay.Data.Import
open Namastay.IntegrationTests
open Namastay.IntegrationTests.Support
open Namastay.Web.Http

type DataCacheTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    let start (versionLifetime: TimeSpan) =
        task {
            let! db = scope.NewDatabase()
            migrated db
            Assert.True((Run.run db Run.Apply "first" Sample.catalog).IsOk)
            let source = scope.Own(NpgsqlDataSource.Create db)
            return scope.Own(DataCache.create versionLifetime source), db
        }

    [<Fact>]
    member _.``requests that miss together share one load``() =
        task {
            let! data, _ = start (TimeSpan.FromHours 1.0)
            let! _ = DataCache.version data
            let release = TaskCompletionSource<string>()
            let loads = ref 0

            let load () =
                Interlocked.Increment loads |> ignore
                release.Task

            let waiting = [ for _ in 1..5 -> DataCache.getOrLoad data "shared" load ]
            release.SetResult "loaded"
            let! answers = Task.WhenAll waiting
            Assert.Equal<string list>(List.replicate 5 "loaded", List.ofArray answers)
            Assert.Equal(1, loads.Value)
        }

    [<Fact>]
    member _.``a failed load is tried again``() =
        task {
            let! data, _ = start (TimeSpan.FromHours 1.0)

            let! _ =
                Assert.ThrowsAsync<InvalidOperationException>(fun () ->
                    DataCache.getOrLoad data "flaky" (fun () ->
                        Task.FromException<string>(InvalidOperationException())))

            let! answer = DataCache.getOrLoad data "flaky" (fun () -> Task.FromResult "loaded")
            Assert.Equal("loaded", answer)
        }

    [<Fact>]
    member _.``the version is remembered for its lifetime``() =
        task {
            let! data, db = start (TimeSpan.FromHours 1.0)
            let! before = DataCache.version data
            Assert.True((Run.run db Run.Apply "second" Sample.catalog).IsOk)
            let! after = DataCache.version data
            Assert.Equal(before, after)
        }

    [<Fact>]
    member _.``a new import shows once the version has expired``() =
        task {
            let! data, db = start TimeSpan.Zero
            let! before = DataCache.version data
            Assert.True((Run.run db Run.Apply "second" Sample.catalog).IsOk)
            let! after = DataCache.version data
            Assert.True(after > before)
        }

    interface IClassFixture<PostgresFixture>
