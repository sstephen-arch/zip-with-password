import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import { PDFDocument, rgb, StandardFonts } from "https://esm.sh/pdf-lib@1.17.1";

// ── Environment ───────────────────────────────────────────────────────────────
const SUPABASE_URL     = Deno.env.get("SUPABASE_URL")!;
const SUPABASE_SVC_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const RESEND_API_KEY   = Deno.env.get("RESEND_API_KEY")!;
const FROM_EMAIL       = Deno.env.get("FROM_EMAIL") ?? "notifications@starkive.app";

// ── Handler ───────────────────────────────────────────────────────────────────
Deno.serve(async (req: Request) => {
  if (req.method !== "POST") return new Response("Method not allowed", { status: 405 });

  // Parse body
  let body: { file_token?: string; star_name?: string; file_name?: string };
  try { body = await req.json(); }
  catch { return new Response("Bad JSON", { status: 400 }); }

  const { file_token, star_name, file_name } = body;
  if (!file_token) return new Response("Missing file_token", { status: 400 });

  // Service-role client — bypasses RLS on all public tables
  const supabase = createClient(SUPABASE_URL, SUPABASE_SVC_KEY, {
    auth: { persistSession: false, autoRefreshToken: false },
  });

  // ── 1. Look up the SSZ file ──────────────────────────────────────────────
  const { data: file, error: fileErr } = await supabase
    .from("ssz_files")
    .select("id, owner_id, original_filename, recipient_hint, open_count, star_name")
    .eq("file_token", file_token)
    .maybeSingle();

  if (fileErr) console.error("[report-open] ssz_files lookup error:", JSON.stringify(fileErr));
  if (!file)   return new Response("ok", { status: 200 }); // unknown token — silent ok

  const now      = new Date().toISOString();
  const newCount = (file.open_count ?? 0) + 1;
  const opener_ip  = req.headers.get("x-forwarded-for")?.split(",")[0].trim() ?? null;
  const user_agent = req.headers.get("user-agent") ?? null;

  // ── 2. Increment open counter (fire-and-forget) ──────────────────────────
  supabase.from("ssz_files")
    .update({ open_count: newCount, last_opened_at: now })
    .eq("id", file.id)
    .then(({ error: e }) => { if (e) console.error("[report-open] open_count update error:", JSON.stringify(e)); });

  // ── 3. Insert audit event ────────────────────────────────────────────────
  let auditId: string | null = null;
  try {
    const { data: ae, error: aeErr } = await supabase
      .from("audit_events")
      .insert({ file_id: file.id, event_type: "file_opened", ip_address: opener_ip, user_agent, opened_at: now })
      .select("id")
      .single();
    if (aeErr) console.error("[report-open] audit_events insert error:", JSON.stringify(aeErr));
    auditId = ae?.id ?? null;
  } catch (e) { console.error("[report-open] audit_events exception:", e); }

  // ── 4. Get owner email via Admin API (reads auth.users — always accessible) ──
  let ownerEmail: string | null = null;
  try {
    const { data: adminData, error: adminErr } = await supabase.auth.admin.getUserById(file.owner_id);
    if (adminErr) console.error("[report-open] getUserById error:", JSON.stringify(adminErr));
    ownerEmail = adminData?.user?.email ?? null;
  } catch (e) { console.error("[report-open] getUserById exception:", e); }

  // ── 5. Guard: need email + API key to proceed ────────────────────────────
  if (!ownerEmail) {
    console.error("[report-open] No email found for owner_id:", file.owner_id);
    return new Response("ok", { status: 200 });
  }
  if (!RESEND_API_KEY) {
    console.error("[report-open] RESEND_API_KEY not configured");
    return new Response("ok", { status: 200 });
  }

  // ── 6. Resolve display values ────────────────────────────────────────────
  const resolvedStar = file.star_name ?? star_name ?? "Unknown";
  const resolvedFile = file.original_filename ?? file_name ?? "Unnamed file";
  const openTime     = new Date(now).toUTCString();

  // ── 7. Build PDF certificate (failure does NOT block email) ──────────────
  let pdfBase64: string | null = null;
  try {
    const pdfBytes = await buildNotificationPdf(resolvedFile, resolvedStar, openTime, newCount, file.recipient_hint ?? null);
    pdfBase64 = encodeBase64(pdfBytes);
  } catch (e) { console.error("[report-open] PDF build error:", e); }

  // ── 8. Build email HTML ──────────────────────────────────────────────────
  const recipientLine = file.recipient_hint
    ? `<p style="margin:0 0 8px"><strong>Opened by:</strong> ${escHtml(file.recipient_hint)}</p>`
    : "";

  const html = `
<div style="font-family:'Segoe UI',Arial,sans-serif;max-width:540px;margin:0 auto;color:#1a2535">
  <div style="background:#080C14;padding:20px 28px;border-radius:8px 8px 0 0;border-bottom:3px solid #2563EB">
    <span style="color:#E8F0FB;font-size:18px;font-weight:600">&#9733; Starkive</span>
    <span style="color:#4A7CCC;font-size:13px;margin-left:10px">Open Notification</span>
  </div>
  <div style="background:#f9fafb;padding:24px 28px;border-radius:0 0 8px 8px;border:1px solid #e5e7eb;border-top:none">
    <p style="margin:0 0 16px;font-size:15px;color:#111827">Your secure container was opened.</p>
    <div style="background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:16px 20px;margin-bottom:16px">
      <p style="margin:0 0 10px"><strong>File:</strong> ${escHtml(resolvedFile)}</p>
      <p style="margin:0 0 10px"><strong>Star identity:</strong> &#9733; ${escHtml(resolvedStar)}</p>
      ${recipientLine}
      <p style="margin:0 0 10px"><strong>Time:</strong> ${openTime}</p>
      <p style="margin:0"><strong>Total opens:</strong> ${newCount}</p>
    </div>
    ${pdfBase64 ? '<p style="margin:0 0 8px;font-size:12px;color:#6b7280">A PDF open certificate is attached to this email.</p>' : ""}
    <p style="margin:0;font-size:12px;color:#6b7280">You are receiving this because you created a Starkive protected file.</p>
  </div>
</div>`;

  // ── 9. Send email via Resend ─────────────────────────────────────────────
  const emailPayload: Record<string, unknown> = {
    from:    `Starkive <${FROM_EMAIL}>`,
    to:      [ownerEmail],
    subject: `[Starkive] ${resolvedStar} - "${resolvedFile}" was opened`,
    html,
  };
  if (pdfBase64) {
    emailPayload.attachments = [{
      filename: `Starkive_${resolvedStar}_Certificate.pdf`,
      content:  pdfBase64,
    }];
  }

  try {
    const emailResp = await fetch("https://api.resend.com/emails", {
      method:  "POST",
      headers: { "Authorization": `Bearer ${RESEND_API_KEY}`, "Content-Type": "application/json" },
      body:    JSON.stringify(emailPayload),
    });

    if (emailResp.ok) {
      // Mark notified
      if (auditId) {
        const { error: nErr } = await supabase.from("audit_events").update({ notified_at: now }).eq("id", auditId);
        if (nErr) console.error("[report-open] notified_at update error:", JSON.stringify(nErr));
      }
      console.log(`[report-open] Email sent to ${ownerEmail} for star=${resolvedStar}`);
    } else {
      const errBody = await emailResp.text();
      console.error(`[report-open] Resend error ${emailResp.status}: ${errBody}`);
    }
  } catch (e) { console.error("[report-open] Email send exception:", e); }

  return new Response("ok", { status: 200 });
});

// ── PDF certificate ───────────────────────────────────────────────────────────
// IMPORTANT: Only use WinAnsi-safe characters (ASCII printable + Latin-1).
// No Unicode stars, em-dashes, or special symbols — they crash pdf-lib's standard fonts.

async function buildNotificationPdf(
  fileName:      string,
  starName:      string,
  openTime:      string,
  openCount:     number,
  recipientHint: string | null,
): Promise<Uint8Array> {
  const doc  = await PDFDocument.create();
  const page = doc.addPage([595, 420]); // A5 landscape
  const { width, height } = page.getSize();

  const fontBold    = await doc.embedFont(StandardFonts.HelveticaBold);
  const fontRegular = await doc.embedFont(StandardFonts.Helvetica);

  // Background
  page.drawRectangle({ x: 0, y: 0, width, height, color: rgb(0.039, 0.055, 0.098) });

  // Top accent bar
  page.drawRectangle({ x: 0, y: height - 5, width, height: 5, color: rgb(0.149, 0.357, 0.922) });

  // Header: "* STARKIVE" (ASCII * instead of Unicode star)
  page.drawText("*", {
    x: 48, y: height - 58, size: 26, font: fontBold,
    color: rgb(0.576, 0.773, 0.965),
  });
  page.drawText("STARKIVE", {
    x: 72, y: height - 54, size: 22, font: fontBold,
    color: rgb(0.910, 0.941, 0.984),
  });
  page.drawText("OPEN NOTIFICATION CERTIFICATE", {
    x: 72, y: height - 72, size: 9, font: fontRegular,
    color: rgb(0.400, 0.557, 0.722),
  });

  // Divider
  page.drawLine({
    start: { x: 48, y: height - 88 }, end: { x: width - 48, y: height - 88 },
    thickness: 0.5, color: rgb(0.15, 0.20, 0.30),
  });

  // Star name (large, centred) — safe: star names are ASCII letters
  const starFontSize = 40;
  const starWidth    = fontBold.widthOfTextAtSize(starName, starFontSize);
  page.drawText(starName, {
    x: (width - starWidth) / 2, y: height - 148,
    size: starFontSize, font: fontBold,
    color: rgb(0.910, 0.941, 0.984),
  });
  const subLabel = "Star Identity";
  const subWidth = fontRegular.widthOfTextAtSize(subLabel, 11);
  page.drawText(subLabel, {
    x: (width - subWidth) / 2, y: height - 166,
    size: 11, font: fontRegular,
    color: rgb(0.400, 0.557, 0.722),
  });

  // Detail rows
  const rows: [string, string][] = [
    ["File",       sanitizeForPdf(fileName)],
    ["Opened at",  openTime],
    ["Open count", openCount.toString()],
  ];
  if (recipientHint) rows.splice(1, 0, ["Opened by", sanitizeForPdf(recipientHint)]);

  let rowY = height - 212;
  for (const [label, value] of rows) {
    page.drawText(label + ":", {
      x: 48, y: rowY, size: 10, font: fontBold,
      color: rgb(0.400, 0.557, 0.722),
    });
    // Truncate long values to fit the page
    const maxW = width - 48 - 160;
    let display = value;
    while (display.length > 4 && fontRegular.widthOfTextAtSize(display, 10) > maxW) {
      display = display.slice(0, -4) + "...";
    }
    page.drawText(display, {
      x: 162, y: rowY, size: 10, font: fontRegular,
      color: rgb(0.910, 0.941, 0.984),
    });
    rowY -= 22;
  }

  // Footer
  page.drawLine({
    start: { x: 48, y: 38 }, end: { x: width - 48, y: 38 },
    thickness: 0.5, color: rgb(0.15, 0.20, 0.30),
  });
  page.drawText("Generated by Starkive  |  starkive.app", {
    x: 48, y: 24, size: 8, font: fontRegular,
    color: rgb(0.30, 0.40, 0.55),
  });

  return await doc.save();
}

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Strip any character outside WinAnsi (printable Latin-1) to avoid pdf-lib crashes. */
function sanitizeForPdf(s: string): string {
  // Replace common Unicode to ASCII equivalents, then strip anything > U+00FF
  return s
    .replace(/[—–]/g, "-")   // em/en dash
    .replace(/[‘’]/g, "'")   // curly apostrophes
    .replace(/[“”]/g, '"')   // curly quotes
    .replace(/[^\x20-\x7E\xA0-\xFF]/g, "?"); // everything else → ?
}

function encodeBase64(bytes: Uint8Array): string {
  let binary = "";
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary);
}

function escHtml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}
