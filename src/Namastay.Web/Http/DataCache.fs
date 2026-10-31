namespace Namastay.Web.Http

open System
open System.Threading.Tasks
open Microsoft.Extensions.Caching.Memory
open Npgsql
open Namastay.Data

/// Travel data only changes through imports, so entries are keyed by the latest
/// committed import. A new import makes every old entry unreachable at once.
type DataCache = {
    Source: NpgsqlDataSource
    Memory: MemoryCache
} with

    interface IDisposable with
        member this.Dispose() = this.Memory.Dispose()

[<RequireQualifiedAccess>]
module DataCache =
    let private unusedEntryLifetime = TimeSpan.FromHours 1.0

    let create (source: NpgsqlDataSource) = {
        Source = source
        Memory = new MemoryCache(MemoryCacheOptions())
    }

    let private store (data: DataCache) (key: string) (value: 'Value) =
        data.Memory.Set(key, box value, MemoryCacheEntryOptions(SlidingExpiration = unusedEntryLifetime))
        |> ignore

    let version (data: DataCache) : Task<int64> =
        task {
            let! rows =
                Sql.query<int64>
                    data.Source
                    "select coalesce(max(id), 0) from import_batches where status = 'committed'"
                    null

            return List.head rows
        }

    /// Loads once per import.
    let getOrLoad (data: DataCache) (name: string) (load: unit -> Task<'Value>) : Task<'Value> =
        task {
            let! current = version data
            let key = $"{name}:{current}"

            match data.Memory.TryGetValue key with
            | true, value -> return unbox<'Value> value
            | _ ->
                let! value = load ()
                store data key value
                return value
        }

    /// Keeps only real results, so random addresses can't fill the cache.
    let getOrLoadOption (data: DataCache) (name: string) (load: unit -> Task<'Value option>) : Task<'Value option> =
        task {
            let! current = version data
            let key = $"{name}:{current}"

            match data.Memory.TryGetValue key with
            | true, value -> return Some(unbox<'Value> value)
            | _ ->
                let! value = load ()
                value |> Option.iter (store data key)
                return value
        }
