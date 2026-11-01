alter table places add column food_intro text;

create table dishes (
    place_slug text not null references places (slug),
    name text not null,
    description text not null,
    vegetarian boolean not null,
    status text not null,
    last_checked date not null,
    primary key (place_slug, name)
);

create table eateries (
    place_slug text not null references places (slug),
    name text not null,
    area text not null,
    go_for text not null,
    cost_level text not null,
    vegetarian_only boolean not null,
    status text not null,
    last_checked date not null,
    primary key (place_slug, name)
);
