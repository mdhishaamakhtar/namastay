/// How many travellers began the deck each day. A count, with nothing about who.
module Namastay.Data.Journey.Starts

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql

let record (source: NpgsqlDataSource) : Task<int> =
    execute
        source
        "insert into journey_starts (day, starts) values (current_date, 1)
         on conflict (day) do update set starts = journey_starts.starts + 1"
        null
