module Namastay.Data.Migrate

open System.Reflection
open DbUp

let private upgrader (connectionString: string) =
    DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .WithTransaction()
        .LogToNowhere()
        .Build()

/// Whether a database is behind the migrations in this build.
let isBehind (connectionString: string) =
    (upgrader connectionString).IsUpgradeRequired()

let run (connectionString: string) : Result<unit, string> =
    let result = (upgrader connectionString).PerformUpgrade()

    if result.Successful then
        Ok()
    else
        Error result.Error.Message
