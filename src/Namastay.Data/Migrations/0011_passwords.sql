alter table users add column password_hash text;
alter table users drop constraint users_provider_check;
alter table users add constraint users_provider_check check (provider in ('password', 'google'));
create unique index users_email_lower on users (lower(email));
