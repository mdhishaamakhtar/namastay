create index experiences_place on experiences (place_slug) where retired_at is null;
create index notices_scope on notices (scope, scope_ref);
create index card_signals_card on card_signals (card_slug);
create index transport_links_live on transport_links (from_slug, to_slug) where retired_at is null;
create index import_batches_committed on import_batches (id desc) where status = 'committed';
