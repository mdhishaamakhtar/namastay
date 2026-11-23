module Namastay.Data.Trips.Itineraries

open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql

/// Stores the resolved trip, so a shared link never changes with the data.
let save (source: NpgsqlDataSource) (token: string) (title: string) (trip: string) (version: int) : Task =
    execute
        source
        "insert into itineraries (share_token, title, trip, data_version)
         values (@token, @title, @trip::jsonb, @version)"
        {|
            token = token
            title = title
            trip = trip
            version = version
        |}

let tryLoad (source: NpgsqlDataSource) (token: string) : Task<string option> =
    task {
        let! rows =
            query<string> source "select trip::text from itineraries where share_token = @token" {| token = token |}

        return rows |> List.tryHead
    }

/// Which of these places are no longer in the guide.
let retiredAmong (source: NpgsqlDataSource) (slugs: string list) : Task<string list> =
    query<string>
        source
        "select slug from places where slug = any(@slugs) and (retired_at is not null or not published)"
        {| slugs = List.toArray slugs |}

/// Keeps a traveller's latest answer on whether the trip felt like theirs.
/// False when there's no such trip.
let react (source: NpgsqlDataSource) (token: string) (feltRight: bool) : Task<bool> =
    task {
        let! kept =
            query<string>
                source
                "insert into trip_reactions (share_token, felt_right)
                 select share_token, @feltRight from itineraries where share_token = @token
                 on conflict (share_token) do update set felt_right = excluded.felt_right, reacted_at = now()
                 returning share_token"
                {|
                    token = token
                    feltRight = feltRight
                |}

        return not kept.IsEmpty
    }
