module Namastay.Data.Trips.SavedTrips

open System
open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql

[<CLIMutable>]
type SavedRow = {
    Id: Guid
    ShareToken: string
    Title: string
    Trip: string
    SavedOn: string
}

let private columns =
    "id, share_token, title, trip::text as trip, saved_at::date::text as saved_on"

/// Copies the shared trip to the user's account. Saving the same trip again changes nothing.
let tryCopy (source: NpgsqlDataSource) (userId: Guid) (token: string) : Task<SavedRow option> =
    task {
        let! rows =
            query<SavedRow>
                source
                $"insert into saved_trips (user_id, share_token, title, trip)
                  select @userId, share_token, title, trip from itineraries where share_token = @token
                  on conflict (user_id, share_token) do update set user_id = excluded.user_id
                  returning {columns}"
                {| userId = userId; token = token |}

        return List.tryHead rows
    }

let listFor (source: NpgsqlDataSource) (userId: Guid) : Task<SavedRow list> =
    query<SavedRow> source $"select {columns} from saved_trips where user_id = @userId order by saved_at desc, id" {|
        userId = userId
    |}

let tryFind (source: NpgsqlDataSource) (userId: Guid) (id: Guid) : Task<SavedRow option> =
    task {
        let! rows =
            query<SavedRow> source $"select {columns} from saved_trips where id = @id and user_id = @userId" {|
                id = id
                userId = userId
            |}

        return List.tryHead rows
    }

/// Changes only this user's copy. The shared itinerary is untouched.
let tryUpdate (source: NpgsqlDataSource) (userId: Guid) (id: Guid) (title: string) (trip: string) : Task<bool> =
    task {
        let! changed =
            execute
                source
                "update saved_trips set title = @title, trip = @trip::jsonb
                 where id = @id and user_id = @userId"
                {|
                    id = id
                    userId = userId
                    title = title
                    trip = trip
                |}

        return changed > 0
    }

/// True when a trip of this user's was removed. The shared itinerary is untouched.
let delete (source: NpgsqlDataSource) (userId: Guid) (id: Guid) : Task<bool> =
    task {
        let! removed =
            execute source "delete from saved_trips where id = @id and user_id = @userId" {| id = id; userId = userId |}

        return removed > 0
    }
