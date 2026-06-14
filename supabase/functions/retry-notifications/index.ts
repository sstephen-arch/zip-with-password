import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL     = Deno.env.get("SUPABASE_URL")!;
const SUPABASE_SVC_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const RESEND_API_KEY   = Deno.env.get("RESEND_API_KEY")!;
const FROM_EMAIL       = Deno.env.get("FROM_EMAIL") ?? "notifications@starkive.app";

interface PendingNotification {
  id: string;
  audit_event_id: string | null;
  owner_email: string;
  subject: string;
  html_body: string;
  pdf_base64: string | null;
  pdf_filename: string | null;
  attempts: number;
}

Deno.serve(async (req: Request) => {
  // Allow GET (cron) or POST (manual trigger)
  if (req.method !== "GET" && req.method !== "POST")
    return new Response("Method not allowed", { status: 405 });

  const supabase = createClient(SUPABASE_URL, SUPABASE_SVC_KEY, {
    auth: { persistSession: false, autoRefreshToken: false },
  });

  // Fetch up to 10 unresolved rows with fewer than 10 attempts, oldest first
  const { data: rows, error: fetchErr } = await supabase
    .from("pending_notifications")
    .select("id, audit_event_id, owner_email, subject, html_body, pdf_base64, pdf_filename, attempts")
    .is("resolved_at", null)
    .lt("attempts", 10)
    .order("created_at", { ascending: true })
    .limit(10);

  if (fetchErr) {
    console.error("[retry-notifications] fetch error:", JSON.stringify(fetchErr));
    return new Response(JSON.stringify({ error: fetchErr.message }), { status: 500 });
  }

  const pending = (rows ?? []) as PendingNotification[];
  console.log(`[retry-notifications] Processing ${pending.length} pending notification(s)`);

  const results: { id: string; success: boolean }[] = [];

  for (const row of pending) {
    const emailPayload: Record<string, unknown> = {
      from:    `Starkive <${FROM_EMAIL}>`,
      to:      [row.owner_email],
      subject: row.subject,
      html:    row.html_body,
    };
    if (row.pdf_base64 && row.pdf_filename) {
      emailPayload.attachments = [{
        filename: row.pdf_filename,
        content:  row.pdf_base64,
      }];
    }

    let sent = false;
    try {
      const resp = await fetch("https://api.resend.com/emails", {
        method:  "POST",
        headers: { "Authorization": `Bearer ${RESEND_API_KEY}`, "Content-Type": "application/json" },
        body:    JSON.stringify(emailPayload),
      });

      if (resp.ok) {
        sent = true;
        console.log(`[retry-notifications] Sent id=${row.id} to ${row.owner_email}`);
      } else {
        const errBody = await resp.text();
        console.error(`[retry-notifications] Resend HTTP ${resp.status} for id=${row.id}: ${errBody}`);
      }
    } catch (e) {
      console.error(`[retry-notifications] Resend exception for id=${row.id}:`, e);
    }

    if (sent) {
      const { error: resolveErr } = await supabase
        .from("pending_notifications")
        .update({ resolved_at: new Date().toISOString() })
        .eq("id", row.id);
      if (resolveErr) console.error(`[retry-notifications] resolve update error id=${row.id}:`, JSON.stringify(resolveErr));
    } else {
      const { error: incrErr } = await supabase
        .from("pending_notifications")
        .update({ attempts: row.attempts + 1 })
        .eq("id", row.id);
      if (incrErr) console.error(`[retry-notifications] attempts increment error id=${row.id}:`, JSON.stringify(incrErr));
    }

    results.push({ id: row.id, success: sent });
  }

  return new Response(JSON.stringify({ processed: results.length, results }), {
    status: 200, headers: { "Content-Type": "application/json" },
  });
});
