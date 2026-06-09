import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import { PDFDocument, rgb, StandardFonts } from "https://esm.sh/pdf-lib@1.17.1";

const SUPABASE_URL     = Deno.env.get("SUPABASE_URL")!;
const SUPABASE_SVC_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const RESEND_API_KEY   = Deno.env.get("RESEND_API_KEY")!;
const FROM_EMAIL       = Deno.env.get("FROM_EMAIL") ?? "notifications@starkive.com";

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") return new Response("Method not allowed", { status: 405 });

  let body: { file_token?: string; star_name?: string; file_name?: string };
  try { body = await req.json(); } catch { return new Response("Bad JSON", { status: 400 }); }

  const { file_token, star_name, file_name } = body;
  if (!file_token) return new Response("Missing file_token", { status: 400 });

  const supabase = createClient(SUPABASE_URL, SUPABASE_SVC_KEY);

  // ── Look up the SSZ file record ──────────────────────────────────────────
  const { data: file } = await supabase
    .from("ssz_files")
    .select("id, owner_id, original_filename, recipient_hint, open_count, star_name")
    .eq("file_token", file_token)
    .maybeSingle();

  if (!file) return new Response("ok", { status: 200 });

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
    .update({ open_count: (file.open_count ?? 0) + 1, last_opened_at: now })
    .eq("id", file.id);

  // ── Fetch owner email ────────────────────────────────────────────────────
  const { data: profile } = await supabase
    .from("user_profiles")
    .select("email")
    .eq("id", file.owner_id)
    .maybeSingle();

  const ownerEmail = profile?.email;

  // ── Resolve star name ────────────────────────────────────────────────────
  // Prefer the one from the DB record; fall back to what the client sent
  const resolvedStar = file.star_name || star_name || "Unknown";
  const resolvedFile = file.original_filename || file_name || "Unnamed file";
  const openTime     = new Date(now).toUTCString();
  const openCount    = (file.open_count ?? 0) + 1;

  // ── Send email with PDF attachment ───────────────────────────────────────
  if (ownerEmail && RESEND_API_KEY) {
    const pdfBytes  = await buildNotificationPdf(resolvedFile, resolvedStar, openTime, openCount, file.recipient_hint);
    const pdfBase64 = encodeBase64(pdfBytes);

    const recipientLine = file.recipient_hint
      ? `<p style="margin:0 0 8px"><strong>Opened by:</strong> ${escHtml(file.recipient_hint)}</p>`
      : "";

    const html = `
<div style="font-family:'Segoe UI',sans-serif;max-width:520px;margin:0 auto;color:#1a2535">
  <div style="background:#0A0E16;padding:20px 28px;border-radius:8px 8px 0 0">
    <span style="color:#E8F0FB;font-size:18px;font-weight:600">&#9733; Starkive</span>
    <span style="color:#4A7CCC;font-size:13px;margin-left:10px">Open Notification</span>
  </div>
  <div style="background:#f9fafb;padding:24px 28px;border-radius:0 0 8px 8px;border:1px solid #e5e7eb;border-top:none">
    <p style="margin:0 0 16px;font-size:15px">Your secure container was opened.</p>
    <div style="background:#fff;border:1px solid #e5e7eb;border-radius:6px;padding:16px 20px;margin-bottom:16px">
      <p style="margin:0 0 8px"><strong>File:</strong> ${escHtml(resolvedFile)}</p>
      <p style="margin:0 0 8px"><strong>Star identity:</strong> &#9733; ${escHtml(resolvedStar)}</p>
      ${recipientLine}
      <p style="margin:0 0 8px"><strong>Time:</strong> ${openTime}</p>
      <p style="margin:0"><strong>Total opens:</strong> ${openCount}</p>
    </div>
    <p style="margin:0 0 8px;font-size:12px;color:#6b7280">A full PDF certificate is attached to this email.</p>
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
        subject: `★ ${resolvedStar} — "${resolvedFile}" was opened`,
        html,
        attachments: [{
          filename: `Starkive_${resolvedStar}_OpenCertificate.pdf`,
          content:  pdfBase64,
        }],
      }),
    });

    if (emailResp.ok && auditRow?.id) {
      await supabase.from("audit_events").update({ notified_at: now }).eq("id", auditRow.id);
    } else if (!emailResp.ok) {
      const errText = await emailResp.text();
      console.error(`Email send failed: ${emailResp.status} — ${errText}`);
    }
  }

  return new Response("ok", { status: 200 });
});

// ── PDF certificate builder ───────────────────────────────────────────────────

async function buildNotificationPdf(
  fileName: string,
  starName: string,
  openTime: string,
  openCount: number,
  recipientHint: string | null,
): Promise<Uint8Array> {
  const doc  = await PDFDocument.create();
  const page = doc.addPage([595, 420]); // A5 landscape in points
  const { width, height } = page.getSize();

  const fontBold    = await doc.embedFont(StandardFonts.HelveticaBold);
  const fontRegular = await doc.embedFont(StandardFonts.Helvetica);

  // ── Background ──
  page.drawRectangle({ x: 0, y: 0, width, height, color: rgb(0.039, 0.055, 0.098) }); // #0A0E19

  // ── Top accent bar ──
  page.drawRectangle({ x: 0, y: height - 6, width, height: 6, color: rgb(0.102, 0.337, 0.855) }); // #1A56DB

  // ── Star icon + "STARKIVE" header ──
  page.drawText("★", { x: 48, y: height - 60, size: 28, font: fontBold, color: rgb(0.576, 0.773, 0.965) });
  page.drawText("STARKIVE", { x: 82, y: height - 55, size: 22, font: fontBold, color: rgb(0.910, 0.941, 0.984) });
  page.drawText("OPEN NOTIFICATION CERTIFICATE", { x: 82, y: height - 74, size: 9, font: fontRegular, color: rgb(0.400, 0.557, 0.722) });

  // ── Divider ──
  page.drawLine({ start: { x: 48, y: height - 90 }, end: { x: width - 48, y: height - 90 }, thickness: 0.5, color: rgb(0.15, 0.20, 0.30) });

  // ── Star name (large centred) ──
  const starFontSize = 42;
  const starWidth = fontBold.widthOfTextAtSize(starName, starFontSize);
  page.drawText(starName, {
    x: (width - starWidth) / 2, y: height - 155,
    size: starFontSize, font: fontBold, color: rgb(0.910, 0.941, 0.984),
  });
  const subLabel = "Star Identity";
  const subWidth = fontRegular.widthOfTextAtSize(subLabel, 11);
  page.drawText(subLabel, {
    x: (width - subWidth) / 2, y: height - 174,
    size: 11, font: fontRegular, color: rgb(0.400, 0.557, 0.722),
  });

  // ── Detail rows ──
  const rows: [string, string][] = [
    ["File",       fileName],
    ["Opened at",  openTime],
    ["Open count", openCount.toString()],
  ];
  if (recipientHint) rows.splice(1, 0, ["Opened by", recipientHint]);

  let rowY = height - 220;
  for (const [label, value] of rows) {
    page.drawText(label, { x: 48,  y: rowY, size: 10, font: fontBold,    color: rgb(0.400, 0.557, 0.722) });
    page.drawText(value, { x: 160, y: rowY, size: 10, font: fontRegular, color: rgb(0.910, 0.941, 0.984) });
    rowY -= 22;
  }

  // ── Footer ──
  page.drawLine({ start: { x: 48, y: 40 }, end: { x: width - 48, y: 40 }, thickness: 0.5, color: rgb(0.15, 0.20, 0.30) });
  page.drawText("Generated by Starkive · starkive.app", {
    x: 48, y: 26, size: 8, font: fontRegular, color: rgb(0.30, 0.40, 0.55),
  });

  return await doc.save();
}

function encodeBase64(bytes: Uint8Array): string {
  let binary = "";
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary);
}

function escHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
