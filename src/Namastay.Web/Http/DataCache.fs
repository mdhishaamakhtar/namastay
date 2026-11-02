namespace Namastay.Web.Http

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open Microsoft.Extensions.Caching.Memory
open Npgsql
open Namastay.Data

/// Travel data only changes through imports, so entries are keyed by the latest
/// committed import. A new import makes every old entry unreachable at once.
type DataCache = {
    Source: NpgsqlDataSource
    Memory: MemoryCache
    Loading: ConcurrentDictionary<string, Lazy<Task<obj>>>
    VersionLifetime: TimeSpan
} with

    interface IDisposable with
        member this.Dispose() = this.Memory.Dispose()

[<RequireQualifiedAccess>]
module DataCache =
    let private unusedEntryLifetime = TimeSpan.FromHours 1.0

    /// How long a new import can take to show. Every cached read asks for the version.
    let versionLifetime = TimeSpan.FromSeconds 5.0

    let private versionKey = "version"

    let create (versionLifetime: TimeSpan) (source: NpgsqlDataSource) = {
        Source = source
        Memory = new MemoryCache(MemoryCacheOptions())
        Loading = ConcurrentDictionary()
        VersionLifetime = versionLifetime
    }

    let private store (data: DataCache) (key: string) (value: 'Value) =
        data.Memory.Set(key, box value, MemoryCacheEntryOptions(SlidingExpiration = unusedEntryLifetime))
        |> ignore

    let private latestImport (data: DataCache) =
        task {
            let! rows =
                Sql.query<int64>
                    data.Source
                    "select coalesce(max(id), 0) from import_batches where status = 'committed'"
                    null

            let latest = List.head rows

            if data.VersionLifetime > TimeSpan.Zero then
                data.Memory.Set(versionKey, latest, data.VersionLifetime) |> ignore

            return latest
        }

    let version (data: DataCache) : Task<int64> =
        match data.Memory.TryGetValue versionKey with
        | true, value -> Task.FromResult(unbox<int64> value)
        | _ -> latestImport data

    /// Keeps only real results, so random addresses can't fill the cache.
    /// Requests that miss together wait for one load instead of each running their own.
    let getOrLoadOption (data: DataCache) (name: string) (load: unit -> Task<'Value option>) : Task<'Value option> =
        task {
            let! current = version data
            let key = $"{name}:{current}"

            match data.Memory.TryGetValue key with
            | true, value -> return Some(unbox<'Value> value)
            | _ ->
                let work =
                    data.Loading.GetOrAdd(
                        key,
                        fun _ ->
                            lazy
                                (task {
                                    let! value = load ()
                                    value |> Option.iter (store data key)
                                    return box value
                                })
                    )

                try
                    let! value = work.Value
                    return unbox<'Value option> value
                finally
                    data.Loading.TryRemove key |> ignore
        }

    /// Loads once per import.
    let getOrLoad (data: DataCache) (name: string) (load: unit -> Task<'Value>) : Task<'Value> =
        task {
            let! value =
                getOrLoadOption data name (fun () ->
                    task {
                        let! loaded = load ()
                        return Some loaded
                    })

            return Option.get value
        }
