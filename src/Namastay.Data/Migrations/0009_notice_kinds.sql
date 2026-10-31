alter table notices add constraint notices_kind_check
    check (kind in ('closure', 'safety', 'legal', 'permit'));
