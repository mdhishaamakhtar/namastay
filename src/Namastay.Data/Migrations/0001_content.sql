create extension if not exists pg_trgm;

create domain fact_status as text
    check (value in ('official', 'two_sources', 'one_source', 'conflict', 'editorial', 'unverified'));

create table regions (
    slug text primary key,
    name text not null
);

create table clusters (
    slug text primary key,
    region_slug text not null references regions (slug),
    name text not null
);

create table colour_families (
    slug text primary key,
    name text not null,
    used_for text not null,
    deep text not null,
    mid text not null,
    pale text not null,
    text_on_deep text not null default '#FFFFFF'
);

create table places (
    slug text primary key,
    cluster_slug text not null references clusters (slug),
    name text not null,
    state text not null,
    family_slug text not null references colour_families (slug),
    shade text not null check (shade in ('deep', 'mid', 'pale')),
    gate text not null check (gate in ('open', 'warn', 'permit')),
    popularity text not null check (popularity in ('mainstream', 'between', 'offbeat')),
    days_min int not null check (days_min > 0),
    days_max int not null,
    cost_low int,
    cost_high int,
    summary text not null,
    is_this_for_you text,
    published boolean not null default false,
    retired_at timestamptz,
    status fact_status not null,
    last_checked date not null,
    check (days_max >= days_min),
    check (cost_high is null or cost_low is null or cost_high >= cost_low)
);

create index places_cluster on places (cluster_slug);
create index places_name_trgm on places using gin (name gin_trgm_ops);
create index places_search on places
    using gin (to_tsvector('simple', name || ' ' || state || ' ' || summary));

create table interests (
    slug text primary key,
    name text not null,
    parent_slug text references interests (slug)
);

create table place_interests (
    place_slug text not null references places (slug),
    interest_slug text not null references interests (slug),
    weight int not null check (weight between 1 and 5),
    primary key (place_slug, interest_slug)
);

create table place_months (
    place_slug text not null references places (slug),
    month int not null check (month between 1 and 12),
    rating text not null check (rating in ('best', 'ok', 'avoid', 'closed', 'unknown')),
    note text,
    status fact_status not null,
    last_checked date not null,
    primary key (place_slug, month)
);

create table traveller_fit (
    place_slug text not null references places (slug),
    traveller text not null check (traveller in ('solo', 'couple', 'friends', 'family')),
    fit int not null check (fit between 0 and 3),
    primary key (place_slug, traveller)
);

create table experiences (
    slug text primary key,
    place_slug text not null references places (slug),
    name text not null,
    description text not null,
    sort int not null default 0,
    status fact_status not null,
    last_checked date not null
);

create table day_plans (
    place_slug text not null references places (slug),
    variant text not null,
    day int not null check (day > 0),
    plan text not null,
    status fact_status not null,
    last_checked date not null,
    primary key (place_slug, variant, day)
);

create table stay_areas (
    place_slug text not null references places (slug),
    name text not null,
    cost_level text not null check (cost_level in ('budget', 'mid', 'luxury')),
    note text,
    status fact_status not null,
    last_checked date not null,
    primary key (place_slug, name)
);

create table transport_links (
    slug text primary key,
    from_slug text not null references places (slug),
    to_slug text not null references places (slug),
    mode text not null check (mode in ('flight', 'train', 'road', 'ferry', 'cable_car')),
    minutes_min int not null check (minutes_min > 0),
    minutes_max int not null,
    distance_km int,
    run_days text,
    valid_from date,
    valid_to date,
    comfort_note text,
    status fact_status not null,
    last_checked date not null,
    check (minutes_max >= minutes_min),
    check (valid_to is null or valid_from is null or valid_to > valid_from),
    check (from_slug <> to_slug)
);

create index transport_links_from on transport_links (from_slug);

create table trains (
    link_slug text not null references transport_links (slug),
    number text not null,
    departs time,
    arrives time,
    run_days text,
    source_note text,
    status fact_status not null,
    last_checked date not null,
    primary key (link_slug, number)
);

create table festivals (
    slug text primary key,
    name text not null,
    place_slug text references places (slug),
    date_rule text not null,
    visitor_notes text not null,
    status fact_status not null,
    last_checked date not null
);

create table festival_dates (
    festival_slug text not null references festivals (slug),
    year int not null,
    starts date not null,
    ends date not null,
    is_moon_dependent boolean not null default false,
    primary key (festival_slug, year),
    check (ends >= starts)
);

create table notices (
    slug text primary key,
    scope text not null check (scope in ('national', 'state', 'place', 'link', 'festival')),
    scope_ref text,
    kind text not null,
    severity text not null check (severity in ('info', 'warn', 'permit', 'danger')),
    body text not null,
    valid_from date,
    valid_to date,
    status fact_status not null,
    last_checked date not null,
    check (valid_to is null or valid_from is null or valid_to > valid_from)
);

create table exclusions (
    slug text primary key,
    name text not null,
    reason text not null
);
