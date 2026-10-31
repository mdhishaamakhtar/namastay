create table illustrations (
    slug text primary key,
    file_path text not null,
    alt text not null,
    brief text not null,
    status text not null check (status in ('brief', 'draft', 'final'))
);

create table cards (
    slug text primary key,
    place_slug text references places (slug),
    title text not null,
    line text not null,
    illustration_slug text references illustrations (slug),
    is_start boolean not null default false
);

create table card_branches (
    from_card text not null references cards (slug),
    on_answer boolean not null,
    to_card text not null references cards (slug),
    priority int not null default 0,
    primary key (from_card, on_answer, to_card)
);

create table card_signals (
    card_slug text not null references cards (slug),
    signal text not null,
    weight int not null,
    primary key (card_slug, signal)
);
