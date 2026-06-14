-- Trigger: keep user_profiles.is_pro in sync with subscriptions.status.
-- Fires after any INSERT or status UPDATE on subscriptions.
-- active / past_due  → is_pro = true  (past_due = Stripe retry grace period)
-- canceled / anything else → is_pro = false

CREATE OR REPLACE FUNCTION public.sync_pro_from_subscription()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  v_user_id uuid;
BEGIN
  SELECT id INTO v_user_id FROM auth.users WHERE email = NEW.email;
  IF v_user_id IS NULL THEN
    RETURN NEW;
  END IF;

  IF NEW.status IN ('active', 'past_due') THEN
    UPDATE public.user_profiles SET is_pro = true  WHERE id = v_user_id;
  ELSE
    UPDATE public.user_profiles SET is_pro = false WHERE id = v_user_id;
  END IF;

  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sync_pro_from_subscription ON public.subscriptions;

CREATE TRIGGER trg_sync_pro_from_subscription
AFTER INSERT OR UPDATE OF status ON public.subscriptions
FOR EACH ROW EXECUTE FUNCTION public.sync_pro_from_subscription();
