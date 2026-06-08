const FREE_EVENT_LIMIT = 5;
const FREE_BYTES_LIMIT = 2 * 1024 * 1024; // 2MB

function isWithinFreeLimit(user) {
  return (
    user.audit_events_used < FREE_EVENT_LIMIT &&
    user.audit_bytes_used < FREE_BYTES_LIMIT
  );
}

function canLogAuditEvent(user) {
  if (user.plan === 'pro') return true;
  return isWithinFreeLimit(user);
}

module.exports = { FREE_EVENT_LIMIT, FREE_BYTES_LIMIT, isWithinFreeLimit, canLogAuditEvent };
