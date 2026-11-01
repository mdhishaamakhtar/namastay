module Namastay.Data.Accounts.Users

open System
open System.Threading.Tasks
open Npgsql
open Namastay.Data.Sql

[<CLIMutable>]
type UserRow = {
    Id: Guid
    Email: string
    Provider: string
    PasswordHash: string option
}

let private select = "select id, email, provider, password_hash from users"

let tryFindByEmail (source: NpgsqlDataSource) (email: string) : Task<UserRow option> =
    task {
        let! rows =
            query<UserRow> source $"{select} where lower(email) = lower(@email)" {| email = email |}

        return List.tryHead rows
    }

let tryFindById (source: NpgsqlDataSource) (id: Guid) : Task<UserRow option> =
    task {
        let! rows = query<UserRow> source $"{select} where id = @id" {| id = id |}
        return List.tryHead rows
    }

/// Returns None when the email is already taken. The unique index decides, so a race can't create two.
let tryCreate
    (source: NpgsqlDataSource)
    (email: string)
    (provider: string)
    (hash: string option)
    : Task<UserRow option> =
    task {
        let! rows =
            query<UserRow>
                source
                "insert into users (email, provider, password_hash) values (@email, @provider, @hash)
                 on conflict (lower(email)) do nothing
                 returning id, email, provider, password_hash"
                {|
                    email = email
                    provider = provider
                    hash = hash
                |}

        return List.tryHead rows
    }

/// Google has confirmed this email belongs to the person. A password from sign-up goes, because
/// emails aren't verified there and a stranger could have set it first. One added after
/// Google stays, because the confirmed owner chose it.
let claimForGoogle (source: NpgsqlDataSource) (email: string) : Task<UserRow> =
    task {
        let! rows =
            query<UserRow>
                source
                "insert into users (email, provider) values (@email, 'google')
                 on conflict (lower(email)) do update
                 set provider = 'google',
                     password_hash = case when users.provider = 'google' then users.password_hash end
                 returning id, email, provider, password_hash"
                {| email = email |}

        return List.head rows
    }

/// Adds a password to an account that has none. Returns None if it already has one.
let trySetPassword (source: NpgsqlDataSource) (id: Guid) (hash: string) : Task<UserRow option> =
    task {
        let! rows =
            query<UserRow>
                source
                "update users set password_hash = @hash
                 where id = @id and password_hash is null
                 returning id, email, provider, password_hash"
                {| id = id; hash = hash |}

        return List.tryHead rows
    }
