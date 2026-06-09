import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL     = Deno.env.get("SUPABASE_URL")!;
const SUPABASE_SVC_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const RESEND_API_KEY   = Deno.env.get("RESEND_API_KEY")!;
const FROM_EMAIL       = Deno.env.get("FROM_EMAIL") ?? "notifications@starkive.com";

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }

  let body: { file_token?: string };
  try { body = await req.json(); } catch { return new Response("Bad JSON", { status: 400 }); }

  const { file_token } = body;
  if (!file_token) return new Response("Missing file_token", { status: 400 });

  const supabase = createClient(SUPABASE_URL, SUPABASE_SVC_KEY);

  // ── Look up the SSZ file record ──────────────────────────────────────────
  const { data: file } = await supabase
    .from("ssz_files")
    .select("id, owner_id, original_filename, recipient_hint, open_count")
    .eq("file_token", file_token)
    .maybeSingle();

  if (!file) {
    // Unknown token — return 200 so the desktop app never surfaces an error
    return new Response("ok", { status: 200 });
  }

  const now = new Date().toISOString();

  // ── Insert audit event ───────────────────────────────────────────────────
  const opener_ip = req.headers.get("x-forwarded-for")?.split(",")[0].trim() ?? null;
  const user_agent = req.headers.get("user-agent") ?? null;

  const { data: auditRow } = await supabase.from("audit_events").insert({
    file_id:    file.id,
    event_type: "file_opened",
    ip_address: opener_ip,
    user_agent,
    opened_at:  now,
  }).select("id").single();

  // ── Increment open counter ───────────────────────────────────────────────
  await supabase
    .from("ssz_files")
    .update({
      open_count:     (file.open_count ?? 0) + 1,
      last_opened_at: now,
    })
    .eq("id", file.id);

  // ── Fetch owner email ────────────────────────────────────────────────────
  const { data: profile } = await supabase
    .from("user_profiles")
    .select("email")
    .eq("id", file.owner_id)
    .maybeSingle();

  const ownerEmail = profile?.email;

  // ── Send email via Resend ────────────────────────────────────────────────
  if (ownerEmail && RESEND_API_KEY) {
    const recipientLine = file.recipient_hint
      ? `<p style="margin:0 0 8px"><strong>Opened by:</strong> ${escHtml(file.recipient_hint)}</p>`
      : "";

    const html = `
<div style="font-family:'Segoe UI',sans-serif;max-width:520px;margin:0 auto;color:#1a2535">
  <div style="background:#0A0E16;padding:20px 28px;border-radius:8px 8px 0 0">
    <span style="color:#E8F0FB;font-size:18px;font-weight:600">&#9733; Starkive</span>
  </div>
  <div style="background:#f9fafb;padding:24px 28px;border-radius:0 0 8px 8px;border:1px solid #e5e7eb;border-top:none">
    <p style="margin:0 0 16px;font-size:15px">Your file was opened.</p>
    <div style="background:#fff;border:1px solid #e5e7eb;border-radius:6px;padding:16px 20px;margin-bottom:16px">
      <p style="margin:0 0 8px"><strong>File:</strong> ${escHtml(file.original_filename)}</p>
      ${recipientLine}
      <p style="margin:0"><strong>Time:</strong> ${new Date(now).toUTCString()}</p>
    </div>
    <p style="margin:0;font-size:12px;color:#6b7280">You're receiving this because you created a Starkive protected file.</p>
  </div>
</div>`;

    const emailResp = await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${RESEND_API_KEY}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        from:    `Starkive Notifications <${FROM_EMAIL}>`,
        to:      [ownerEmail],
        subject: `Your file "${file.original_filename}" was opened`,
        html,
      }),
    });

    // Mark notified_at if email sent successfully
    if (emailResp.ok && auditRow?.id) {
      await supabase
        .from("audit_events")
        .update({ notified_at: now })
        .eq("id", auditRow.id);
    }
  }

  return new Response("ok", { status: 200 });
});

function escHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
