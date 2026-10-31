module Namastay.Data.Migrate

open System.Reflection
open DbUp

let run (connectionString: string) : Result<unit, string> =
    let result =
        DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransaction()
            .LogToNowhere()
            .Build()
            .PerformUpgrade()

    if result.Successful then Ok() else Error result.Error.Message
