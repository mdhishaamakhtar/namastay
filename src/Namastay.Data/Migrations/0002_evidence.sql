create table sources (
    id int primary key,
    url text not null unique,
    title text not null,
    publisher text not null,
    kind text not null check (kind in ('official', 'primary', 'secondary', 'guide')),
    accessed_on date not null
);

create table fact_sources (
    table_name text not null,
    row_slug text not null,
    field text not null,
    source_id int not null references sources (id),
    primary key (table_name, row_slug, field, source_id)
);

create table import_batches (
    id bigint generated always as identity primary key,
    started_at timestamptz not null,
    finished_at timestamptz,
    file_hash text not null,
    status text not null check (status in ('running', 'committed', 'failed')),
    counts jsonb not null default '{}'
);
