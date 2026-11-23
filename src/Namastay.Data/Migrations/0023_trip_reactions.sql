-- Whether a traveller said a trip felt like theirs. One answer a trip, the latest kept.
create table trip_reactions (
    share_token text primary key references itineraries (share_token),
    felt_right boolean not null,
    reacted_at timestamptz not null default now()
);
