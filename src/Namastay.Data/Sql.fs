module Namastay.Data.Sql

open System
open System.Data
open System.Threading.Tasks
open Dapper
open Npgsql
open Namastay.Domain.Catalog

type private OptionHandler<'Value>() =
    inherit SqlMapper.TypeHandler<'Value option>()

    override _.SetValue(parameter, value) =
        parameter.Value <-
            match value with
            | Some inner -> box inner
            | None -> box DBNull.Value

    override _.Parse value =
        match value with
        | null -> None
        | :? DBNull -> None
        | inner -> Some(inner :?> 'Value)

let private setup =
    lazy
        (DefaultTypeMap.MatchNamesWithUnderscores <- true
         SqlMapper.AddTypeMap(typeof<DateOnly>, DbType.Date)
         SqlMapper.AddTypeMap(typeof<TimeOnly>, DbType.Time)
         SqlMapper.AddTypeHandler(OptionHandler<string>())
         SqlMapper.AddTypeHandler(OptionHandler<int>())
         SqlMapper.AddTypeHandler(OptionHandler<DateOnly>())
         SqlMapper.AddTypeHandler(OptionHandler<TimeOnly>()))

/// Runs a write inside an import transaction.
let exec (transaction: NpgsqlTransaction) (sql: string) (parameters: obj) =
    setup.Force()
    transaction.Connection.Execute(sql, parameters, transaction)

/// Runs a read, filling `'Row` by column name.
let query<'Row> (source: NpgsqlDataSource) (sql: string) (parameters: obj) : Task<'Row list> =
    task {
        setup.Force()
        use! connection = source.OpenConnectionAsync()
        let! rows = connection.QueryAsync<'Row>(sql, parameters)
        return List.ofSeq rows
    }

/// Replaces the sources recorded for one row of a table.
let linkSources transaction (table: string) (slug: string) (evidence: Evidence) =
    exec transaction "delete from fact_sources where table_name = @table and row_slug = @slug" {|
        table = table
        slug = slug
    |}
    |> ignore

    for sourceId in evidence.SourceIds |> List.distinct do
        exec
            transaction
            "insert into fact_sources (table_name, row_slug, field, source_id) values (@table, @slug, '*', @source)"
            {|
                table = table
                slug = slug
                source = sourceId
            |}
        |> ignore
