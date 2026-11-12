/// Sends the local database's travel content to production as one bulk stream, the way a
/// dump and restore would, then writes only the rows that differ. Nothing else is touched.
module Namastay.Data.Import.Publish

open System
open System.Data
open Dapper
open Npgsql
open Namastay.Data.Import.ContentTables

/// The rows of one table that a publish adds, changes or removes, by key.
type TableChange = {
    Table: string
    Added: string list
    Changed: string list
    Removed: string list
}

let private retiredAt = "retired_at"

let private isRetired (table: ContentTable) = table.Removal = Removal.Retire

let private columns (table: ContentTable) =
    table.Key @ table.Data @ (if isRetired table then [ retiredAt ] else [])

let private staged (table: ContentTable) = "staged_" + table.Name

let private listed (names: string list) = String.concat ", " names

let private from (alias: string) (names: string list) =
    names |> List.map (fun column -> $"{alias}.{column}") |> listed

/// Production's row is `r` in every statement, and the local copy's is `s`.
let private inProduction (names: string list) = from "r" names

let private inLocal (names: string list) = from "s" names

let private sameRow (table: ContentTable) =
    table.Key
    |> List.map (fun column -> $"r.{column} = s.{column}")
    |> String.concat " and "

let private key (table: ContentTable) =
    $"concat_ws(':', {inProduction table.Key})"

/// Whether the local copy has this row, as `r`.
let private isStaged (table: ContentTable) =
    $"exists (select 1 from {staged table} s where {sameRow table})"

/// Empty copies of the content tables, which vanish when the transaction ends.
let private stage (transaction: NpgsqlTransaction) =
    let create =
        all
        |> List.map (fun table -> $"create temp table {staged table} (like public.{table.Name}) on commit drop")
        |> String.concat "; "

    transaction.Connection.Execute(create, transaction = transaction) |> ignore

/// Streams one table from the local database into its staged copy, as COPY text.
let private stream (source: NpgsqlConnection) (target: NpgsqlConnection) (table: ContentTable) =
    let names = listed (columns table)

    use reader =
        source.BeginTextExport $"copy (select {names} from public.{table.Name}) to stdout"

    use writer = target.BeginTextImport $"copy {staged table} ({names}) from stdin"
    let buffer = Array.zeroCreate<char> 65536
    let mutable read = reader.Read(buffer, 0, buffer.Length)

    while read > 0 do
        writer.Write(buffer, 0, read)
        read <- reader.Read(buffer, 0, buffer.Length)

let private keysOf (transaction: NpgsqlTransaction) (sql: string) =
    transaction.Connection.Query<string>(sql, transaction = transaction)
    |> List.ofSeq

let private add transaction (table: ContentTable) =
    keysOf
        transaction
        $"insert into public.{table.Name} as r ({listed (columns table)})
          select {inLocal (columns table)} from {staged table} s
          where not exists (select 1 from public.{table.Name} r where {sameRow table})
          returning {key table}"

/// A row kept retired in production keeps the day it was retired.
let private change transaction (table: ContentTable) =
    let set =
        (table.Data |> List.map (fun column -> $"{column} = s.{column}"))
        @ (if isRetired table then
               [
                   $"{retiredAt} = case when s.{retiredAt} is null then null else coalesce(r.{retiredAt}, s.{retiredAt}) end"
               ]
           else
               [])

    let differs =
        (if table.Data.IsEmpty then
             []
         else
             [ $"({inProduction table.Data}) is distinct from ({inLocal table.Data})" ])
        @ (if isRetired table then
               [ $"(r.{retiredAt} is null) <> (s.{retiredAt} is null)" ]
           else
               [])

    match set with
    | [] -> []
    | _ ->
        keysOf
            transaction
            $"""update public.{table.Name} r set {listed set}
                from {staged table} s
                where {sameRow table} and ({String.concat " or " differs})
                returning {key table}"""

let private remove transaction (table: ContentTable) =
    match table.Removal with
    | Removal.Keep -> []
    | Removal.Retire ->
        keysOf
            transaction
            $"update public.{table.Name} r set {retiredAt} = now()
              where r.{retiredAt} is null and not {isStaged table}
              returning {key table}"
    | Removal.Delete ->
        keysOf transaction $"delete from public.{table.Name} r where not {isStaged table} returning {key table}"
    | Removal.DeleteWithin(column, owner) ->
        let owning = all |> List.find (fun other -> other.Name = owner)

        keysOf
            transaction
            $"delete from public.{table.Name} r
              where not {isStaged table}
                and exists (select 1 from {staged owning} o where o.{owning.Key.Head} = r.{column})
              returning {key table}"

/// Parents are added before what points at them, and removed after.
let private apply (transaction: NpgsqlTransaction) : TableChange list =
    let added = all |> List.map (fun table -> table.Name, add transaction table)
    let changed = all |> List.map (fun table -> table.Name, change transaction table)

    let removed =
        all |> List.rev |> List.map (fun table -> table.Name, remove transaction table)

    let found (rows: (string * string list) list) (name: string) =
        rows |> List.find (fun (table, _) -> table = name) |> snd

    all
    |> List.map (fun table -> {
        Table = table.Name
        Added = found added table.Name
        Changed = found changed table.Name
        Removed = found removed table.Name
    })
    |> List.filter (fun change -> not (change.Added.IsEmpty && change.Changed.IsEmpty && change.Removed.IsEmpty))

/// An empty local database would retire every place in production, so it's refused.
let private hasContent (source: NpgsqlConnection) =
    source.ExecuteScalar<int64> "select count(*) from public.places where retired_at is null" > 0L

/// The data the local database was last imported from, to record against the publish.
let private importedFrom (source: NpgsqlConnection) =
    source.Query<string> "select file_hash from import_batches where status = 'committed' order by id desc limit 1"
    |> Seq.tryHead
    |> Option.defaultValue "published"

let private send (source: NpgsqlConnection) (target: NpgsqlConnection) (mode: Run.Mode) =
    use snapshot = source.BeginTransaction IsolationLevel.RepeatableRead
    use transaction = target.BeginTransaction()
    stage transaction
    all |> List.iter (stream source target)
    let changes = apply transaction

    match mode, changes with
    | Run.DryRun, _ -> transaction.Rollback()
    | Run.Apply, [] -> transaction.Commit()
    | Run.Apply, _ ->
        transaction.Commit()

        // A new batch is how the API learns its data changed.
        changes
        |> List.map (fun change -> change.Table, change.Added.Length + change.Changed.Length + change.Removed.Length)
        |> Map.ofList
        |> Run.recordBatch target (importedFrom source) "committed"

    changes

/// Makes production's travel content match the local database, in one transaction or not
/// at all, and says what changed. A dry run says the same and writes nothing.
let run (source: string) (target: string) (mode: Run.Mode) : Result<TableChange list, string list> =
    try
        use local = new NpgsqlConnection(source)
        use production = new NpgsqlConnection(target)
        local.Open()
        production.Open()

        if hasContent local then
            Ok(send local production mode)
        else
            Error [ "the local database has no places. Run `make import` first." ]
    with
    | :? PostgresException as error -> Error [ $"the database rejected the publish: {error.MessageText}" ]
    | :? NpgsqlException as error -> Error [ $"couldn't reach the database: {error.Message}" ]
