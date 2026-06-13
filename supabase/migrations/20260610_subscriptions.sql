create table if not exists subscriptions (
  id                     uuid primary key default gen_random_uuid(),
  stripe_customer_id     text not null,
  stripe_subscription_id text not null unique,
  email                  text,
  status                 text not null default 'active',
  current_period_end     timestamptz,
  created_at             timestamptz not null default now()
);

create index if not exists subscriptions_email_idx on subscriptions (email);
create index if not exists subscriptions_customer_idx on subscriptions (stripe_customer_id);

-- Row-level security: service role only (webhook writes, app reads via service key)
alter table subscriptions enable row level security;
