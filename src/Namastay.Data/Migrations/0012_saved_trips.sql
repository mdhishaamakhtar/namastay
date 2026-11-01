-- A saved trip is the user's own copy. The shared itinerary stays as it was.
create table saved_trips (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references users (id) on delete cascade,
    share_token text not null references itineraries (share_token),
    title text not null,
    trip jsonb not null,
    saved_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (user_id, share_token)
);

create index saved_trips_user on saved_trips (user_id, saved_at desc);
