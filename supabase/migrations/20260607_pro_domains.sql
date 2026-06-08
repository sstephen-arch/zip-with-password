-- ── Pro domains (enterprise whitelist) ───────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.pro_domains (
  domain      TEXT PRIMARY KEY,               -- e.g. 'acme.com'
  notes       TEXT,                           -- e.g. 'Acme Corp — 50 seats, deal signed 2026-06-07'
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE public.pro_domains ENABLE ROW LEVEL SECURITY;
-- Only service_role (you via Supabase dashboard / Edge Functions) can write.
-- Authenticated users can read — knowing which domains are Pro is not sensitive.
CREATE POLICY pro_domains_read ON public.pro_domains
  FOR SELECT TO authenticated USING (true);

-- ── get_pro_status() RPC ──────────────────────────────────────────────────────
-- Called by the desktop app right after login.
-- Returns true if the user has is_pro=true OR their email domain is in pro_domains.
-- If domain matches and is_pro was false, auto-upgrades the user_profiles row.
CREATE OR REPLACE FUNCTION public.get_pro_status()
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_is_pro  boolean;
  v_email   text;
  v_domain  text;
BEGIN
  -- Check user_profiles first (covers Stripe-paid individual users).
  SELECT is_pro INTO v_is_pro
  FROM public.user_profiles
  WHERE id = auth.uid();

  IF v_is_pro IS TRUE THEN
    RETURN true;
  END IF;

  -- Check pro_domains (enterprise domain whitelist).
  SELECT email INTO v_email
  FROM auth.users
  WHERE id = auth.uid();

  v_domain := lower(split_part(v_email, '@', 2));

  IF EXISTS (SELECT 1 FROM public.pro_domains WHERE domain = v_domain) THEN
    -- Auto-grant Pro so future calls are fast.
    UPDATE public.user_profiles
    SET is_pro = true, pro_started_at = now()
    WHERE id = auth.uid();
    RETURN true;
  END IF;

  RETURN false;
END;
$$;

-- Revoke direct execute from public; only authenticated users can call it.
REVOKE EXECUTE ON FUNCTION public.get_pro_status() FROM public;
GRANT  EXECUTE ON FUNCTION public.get_pro_status() TO authenticated;
