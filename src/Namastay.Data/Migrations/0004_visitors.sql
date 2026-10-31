create table users (
    id uuid primary key default gen_random_uuid(),
    email text not null unique,
    provider text not null check (provider in ('email', 'google')),
    created_at timestamptz not null default now()
);

create table itineraries (
    id uuid primary key default gen_random_uuid(),
    user_id uuid references users (id),
    share_token text not null unique,
    title text not null,
    trip jsonb not null,
    data_version int not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index itineraries_user on itineraries (user_id);
