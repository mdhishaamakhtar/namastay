-- How many travellers answered a first card each day, to read beside the trips built.
create table journey_starts (
    day date primary key,
    starts int not null default 0
);
