ALTER TABLE public.ssz_files
  ADD COLUMN IF NOT EXISTS opened_count INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS public.audit_events (
  id           BIGSERIAL PRIMARY KEY,
  file_token   TEXT NOT NULL,
  owner_id     UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
  event_type   TEXT NOT NULL DEFAULT 'file_opened',
  opener_ip    TEXT,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS audit_events_file_token_idx ON public.audit_events(file_token);
CREATE INDEX IF NOT EXISTS audit_events_owner_id_idx   ON public.audit_events(owner_id);

ALTER TABLE public.audit_events ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_policies WHERE tablename='audit_events' AND policyname='owners_read_own'
  ) THEN
    EXECUTE 'CREATE POLICY owners_read_own ON public.audit_events FOR SELECT USING (owner_id = auth.uid())';
  END IF;
END $$;
