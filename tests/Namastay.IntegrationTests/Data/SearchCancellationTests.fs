namespace Namastay.IntegrationTests.Data

open System
open System.Threading
open Npgsql
open Xunit
open Namastay.Data.Places
open Namastay.IntegrationTests

type SearchCancellationTests(postgres: PostgresFixture) =
    let scope = DatabaseScope.For postgres

    [<Fact>]
    member _.``a search stops when its visitor has gone``() =
        task {
            let! db = scope.NewDatabase()
            migrated db
            let source = scope.Own(NpgsqlDataSource.Create db)
            use gone = new CancellationTokenSource()
            gone.Cancel()

            let! _ =
                Assert.ThrowsAnyAsync<OperationCanceledException>(fun () -> Search.run gone.Token source "goa")

            return ()
        }

    interface IClassFixture<PostgresFixture>
