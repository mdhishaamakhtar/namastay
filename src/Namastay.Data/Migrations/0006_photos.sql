create table photos (
    slug text primary key,
    place_slug text not null references places (slug),
    file_path text not null,
    alt text not null,
    credit text not null,
    licence text not null,
    source_url text not null,
    sort int not null default 0
);

create index photos_place on photos (place_slug);
