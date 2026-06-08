const express = require('express');
const router = express.Router();
const { createObjectCsvWriter } = require('csv-writer');
const path = require('path');
const os = require('os');
const fs = require('fs');
const supabase = require('../db/supabase');
const requireAuth = require('../middleware/requireAuth');
const { canLogAuditEvent } = require('../utils/limits');

// ── POST /audit/events ─────────────────────────────────────────────────────
router.post('/events', requireAuth, async (req, res) => {
  if (!canLogAuditEvent(req.user)) {
    return res.status(403).json({
      error: 'limit_reached',
      upgrade_url: 'starkive://upgrade'
    });
  }

  const {
    event_label,
    created_by,
    category,
    retention_policy,
    notes,
    archive_name,
    file_size_bytes = 0,
    password_mode
  } = req.body;

  const { data: event, error } = await supabase
    .from('audit_events')
    .insert({
      user_id: req.user.id,
      event_label,
      created_by,
      category,
      retention_policy,
      notes,
      archive_name,
      file_size_bytes,
      password_mode
    })
    .select()
    .single();

  if (error) return res.status(500).json({ error: error.message });

  // Increment usage counters
  await supabase
    .from('users')
    .update({
      audit_events_used: req.user.audit_events_used + 1,
      audit_bytes_used: req.user.audit_bytes_used + (file_size_bytes || 0)
    })
    .eq('id', req.user.id);

  res.status(201).json({ event });
});

// ── GET /audit/events ──────────────────────────────────────────────────────
router.get('/events', requireAuth, async (req, res) => {
  const {
    limit = 50,
    offset = 0,
    category,
    from,
    to
  } = req.query;

  let query = supabase
    .from('audit_events')
    .select('*', { count: 'exact' })
    .eq('user_id', req.user.id)
    .order('created_at', { ascending: false })
    .range(Number(offset), Number(offset) + Number(limit) - 1);

  if (category) query = query.eq('category', category);
  if (from) query = query.gte('created_at', from);
  if (to) query = query.lte('created_at', to);

  const { data: events, count, error } = await query;
  if (error) return res.status(500).json({ error: error.message });

  res.json({ events, total: count, limit: Number(limit), offset: Number(offset) });
});

// ── GET /audit/export ──────────────────────────────────────────────────────
router.get('/export', requireAuth, async (req, res) => {
  const { from, to, category } = req.query;

  let query = supabase
    .from('audit_events')
    .select('*')
    .eq('user_id', req.user.id)
    .order('created_at', { ascending: false });

  if (category) query = query.eq('category', category);
  if (from) query = query.gte('created_at', from);
  if (to) query = query.lte('created_at', to);

  const { data: events, error } = await query;
  if (error) return res.status(500).json({ error: error.message });

  const tmpFile = path.join(os.tmpdir(), `starkive-audit-${Date.now()}.csv`);
  const csvWriter = createObjectCsvWriter({
    path: tmpFile,
    header: [
      { id: 'id', title: 'id' },
      { id: 'event_label', title: 'event_label' },
      { id: 'created_by', title: 'created_by' },
      { id: 'category', title: 'category' },
      { id: 'retention_policy', title: 'retention_policy' },
      { id: 'archive_name', title: 'archive_name' },
      { id: 'file_size_bytes', title: 'file_size_bytes' },
      { id: 'password_mode', title: 'password_mode' },
      { id: 'created_at', title: 'created_at' }
    ]
  });

  await csvWriter.writeRecords(events);

  res.setHeader('Content-Type', 'text/csv');
  res.setHeader('Content-Disposition', `attachment; filename="starkive-audit-${Date.now()}.csv"`);
  const stream = fs.createReadStream(tmpFile);
  stream.pipe(res);
  stream.on('end', () => fs.unlink(tmpFile, () => {}));
});

// ── DELETE /audit/events/:id ───────────────────────────────────────────────
router.delete('/events/:id', requireAuth, async (req, res) => {
  const { id } = req.params;

  // Verify ownership
  const { data: event } = await supabase
    .from('audit_events')
    .select('id, user_id')
    .eq('id', id)
    .single();

  if (!event) return res.status(404).json({ error: 'Event not found' });
  if (event.user_id !== req.user.id) return res.status(404).json({ error: 'Event not found' });

  const { error } = await supabase
    .from('audit_events')
    .delete()
    .eq('id', id);

  if (error) return res.status(500).json({ error: error.message });

  res.json({ message: 'Event deleted' });
});

module.exports = router;
