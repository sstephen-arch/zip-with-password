-- Starkive database schema
-- Run this in your Supabase SQL Editor

-- Users table
create table if not exists users (
  id uuid primary key default gen_random_uuid(),
  email text unique not null,
  password_hash text,
  google_id text unique,
  display_name text,
  plan text not null default 'free',
  audit_events_used integer not null default 0,
  audit_bytes_used bigint not null default 0,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

-- Audit events table
create table if not exists audit_events (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references users(id) on delete cascade,
  event_label text,
  created_by text,
  category text,
  retention_policy text,
  notes text,
  archive_name text,
  file_size_bytes bigint default 0,
  password_mode text,
  created_at timestamptz not null default now()
);

-- Indexes
create index if not exists idx_audit_events_user_id on audit_events(user_id);
create index if not exists idx_audit_events_created_at on audit_events(created_at desc);

-- Updated_at trigger
create or replace function update_updated_at()
returns trigger as $$
begin
  new.updated_at = now();
  return new;
end;
$$ language plpgsql;

create trigger users_updated_at
  before update on users
  for each row execute function update_updated_at();
