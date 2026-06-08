/* ── Starkive renderer — app.js ─────────────────────────────────────────── */

'use strict';

// ── EFF short wordlist (embedded, 256 words) ──────────────────────────────
const EFF_WORDS = [
  'able','acid','aged','also','area','army','away','baby','back','ball',
  'band','bank','base','bath','bear','beat','been','bell','best','bill',
  'bird','blow','blue','boat','body','bone','book','born','both','bowl',
  'busy','cake','call','calm','camp','card','care','case','cash','cave',
  'chat','chip','city','clam','clay','clip','club','coat','code','coin',
  'cold','come','cook','copy','cord','core','corn','cost','cozy','crew',
  'crop','cube','cure','cute','dark','data','date','dawn','days','dead',
  'deal','dear','deck','deep','deer','demo','deny','desk','diet','dirt',
  'disk','dock','dome','door','dose','dove','down','draw','drew','drip',
  'drop','drum','dusk','dust','duty','each','earn','ease','east','edge',
  'else','emit','even','ever','exam','face','fact','fade','fair','fall',
  'farm','fast','fate','fawn','fear','feat','feed','feel','fell','felt',
  'file','fill','film','find','fine','fire','firm','fish','fist','five',
  'flag','flat','flew','flip','flow','foam','fold','folk','fond','font',
  'food','foot','ford','fore','fork','form','fort','frog','from','full',
  'fund','fuse','gain','game','gave','gear','gift','glow','glue','goal',
  'gold','golf','good','gown','grab','gram','gray','grew','grid','grin',
  'grip','grow','gust','half','hall','hand','hang','hard','harm','harp',
  'hash','haze','head','heal','heap','heat','held','help','herb','here',
  'hike','hill','hint','hire','hold','hole','home','hood','hook','hope',
  'horn','host','hour','huge','hull','hung','hunt','hurt','icon','idea',
  'idle','inch','into','iron','isle','item','jade','jail','jazz','jest',
  'join','joke','jolt','jump','just','keen','kept','kick','kind','king',
  'knot','know','lace','laid','lake','lamp','land','lane','lark','last',
];

// ── State ─────────────────────────────────────────────────────────────────
const state = {
  mode: 'password',          // 'password' | 'passphrase'
  credential: '',
  sourcePath: null,
  outputPath: null,
  authToken: null,
  user: null,                // { id, email, plan, audit_events_used, audit_bytes_used }
  auditOn: false,
  exhaustedDismissed: false,
};

// ── API helper ────────────────────────────────────────────────────────────
const API_BASE = 'http://localhost:3000';

async function api(method, path, body, token) {
  const headers = { 'Content-Type': 'application/json' };
  if (token || state.authToken) headers['Authorization'] = `Bearer ${token || state.authToken}`;
  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw Object.assign(new Error(data.error || 'API error'), { status: res.status });
  return data;
}

// ── Auth token listener (from main process) ───────────────────────────────
window.starkive.onAuthToken(async (token) => {
  state.authToken = token;
  try {
    const me = await api('GET', '/auth/me');
    setUser(me.user);
  } catch {
    clearAuth();
  }
});

window.starkive.onShowUpgrade(() => {
  showUpgradeModal();
});

// ── User session ──────────────────────────────────────────────────────────
function setUser(user) {
  state.user = user;
  state.authToken = state.authToken || '';
  // Show account chip, hide sign-in link
  document.getElementById('sign-in-link').style.display = 'none';
  document.getElementById('account-chip').style.display = 'flex';
  const initials = (user.display_name || user.email || 'U').charAt(0).toUpperCase();
  document.getElementById('account-avatar').textContent = initials;
  document.getElementById('account-email').textContent = user.email || '';
  if (user.plan === 'pro') {
    document.getElementById('plan-badge').style.display = 'inline';
  }
  renderAuditState();
}

function clearAuth() {
  state.user = null;
  state.authToken = null;
  state.auditOn = false;
  document.getElementById('sign-in-link').style.display = 'inline';
  document.getElementById('account-chip').style.display = 'none';
  document.getElementById('plan-badge').style.display = 'none';
  renderAuditState();
}

function handleAccountChipClick() {
  // Simple: clicking chip offers sign-out
  if (confirm('Sign out of Starkive?')) {
    window.starkive.clearToken();
    clearAuth();
  }
}

// ── Audit state machine ───────────────────────────────────────────────────
// States: no-account | free-idle | free-active | limit-reached | pro-active

function getAuditState(user, toggleOn) {
  if (!user) return 'no-account';
  if (user.plan === 'pro') return toggleOn ? 'pro-active' : 'free-idle'; // pro always can toggle
  const exhausted = user.audit_events_used >= 5;
  if (exhausted) return 'limit-reached';
  return toggleOn ? 'free-active' : 'free-idle';
}

function renderAuditState() {
  const auditState = getAuditState(state.user, state.auditOn);

  const sublabel    = document.getElementById('audit-sublabel');
  const proBadge    = document.getElementById('audit-pro-badge');
  const toggleWrap  = document.getElementById('audit-toggle-wrap');
  const toggleInput = document.getElementById('audit-toggle');
  const usageWrap   = document.getElementById('usage-wrap');
  const fields      = document.getElementById('audit-fields');
  const exhausted   = document.getElementById('audit-exhausted');

  // Reset
  sublabel.style.display   = 'inline';
  proBadge.style.display   = 'none';
  usageWrap.style.display  = 'none';
  fields.classList.remove('visible');
  exhausted.classList.remove('visible');
  toggleWrap.classList.remove('locked');
  toggleInput.disabled = false;

  switch (auditState) {
    case 'no-account':
      sublabel.textContent = 'Sign in to enable';
      toggleInput.checked = false;
      toggleInput.disabled = true;
      toggleWrap.classList.add('locked');
      break;

    case 'free-idle':
      sublabel.textContent = 'Off';
      toggleInput.checked = false;
      renderUsageBar();
      usageWrap.style.display = 'block';
      break;

    case 'free-active':
      sublabel.textContent = 'On · Free';
      toggleInput.checked = true;
      renderUsageBar();
      usageWrap.style.display = 'block';
      fields.classList.add('visible');
      break;

    case 'limit-reached':
      sublabel.style.display = 'none';
      proBadge.style.display = 'inline';
      toggleInput.checked = false;
      toggleInput.disabled = true;
      toggleWrap.classList.add('locked');
      if (!state.exhaustedDismissed) exhausted.classList.add('visible');
      renderUsageBar();
      usageWrap.style.display = 'block';
      break;

    case 'pro-active':
      sublabel.textContent = 'On · Pro';
      toggleInput.checked = true;
      fields.classList.add('visible');
      break;
  }
}

function renderUsageBar() {
  if (!state.user) return;
  const used = state.user.audit_events_used || 0;
  const limit = 5;
  const pct = Math.min(100, (used / limit) * 100);
  document.getElementById('usage-fill').style.width = pct + '%';
  document.getElementById('usage-text').textContent = `${used} / ${limit} events used`;
}

function onAuditToggle() {
  state.auditOn = document.getElementById('audit-toggle').checked;
  renderAuditState();
}

function dismissExhausted() {
  state.exhaustedDismissed = true;
  document.getElementById('audit-exhausted').classList.remove('visible');
}

function goToAuditHistory() {
  // placeholder — future: open a history pane or external page
  alert('Audit history view coming soon in the next release.');
}

function openUpgrade() {
  window.starkive.openGoogleAuth(); // triggers starkive://upgrade via main process
}

function showUpgradeModal() {
  // Reuse sign-up modal but with upgrade messaging
  showSignUpModal();
}

// ── Mode switching ────────────────────────────────────────────────────────
function setMode(mode) {
  state.mode = mode;
  document.getElementById('tab-password').classList.toggle('active', mode === 'password');
  document.getElementById('tab-passphrase').classList.toggle('active', mode === 'passphrase');
  document.getElementById('pane-password').style.display   = mode === 'password'   ? '' : 'none';
  document.getElementById('pane-passphrase').style.display = mode === 'passphrase' ? '' : 'none';
  regen();
}

// ── Password generation ───────────────────────────────────────────────────
const UPPER   = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const LOWER   = 'abcdefghijklmnopqrstuvwxyz';
const NUMBERS = '0123456789';
const SYMBOLS = '!@#$%^&*()-_=+[]{}|;:,.?';

function randomInt(max) {
  const arr = new Uint32Array(1);
  crypto.getRandomValues(arr);
  return arr[0] % max;
}

function generatePassword() {
  const upper   = document.getElementById('opt-upper').checked;
  const lower   = document.getElementById('opt-lower').checked;
  const numbers = document.getElementById('opt-numbers').checked;
  const symbols = document.getElementById('opt-symbols').checked;
  const length  = parseInt(document.getElementById('pw-length').value, 10);

  let charset = '';
  const required = [];

  if (upper)   { charset += UPPER;   required.push(UPPER[randomInt(UPPER.length)]); }
  if (lower)   { charset += LOWER;   required.push(LOWER[randomInt(LOWER.length)]); }
  if (numbers) { charset += NUMBERS; required.push(NUMBERS[randomInt(NUMBERS.length)]); }
  if (symbols) { charset += SYMBOLS; required.push(SYMBOLS[randomInt(SYMBOLS.length)]); }

  if (!charset) { charset = LOWER; }

  const extra = length - required.length;
  const chars = required.slice();
  for (let i = 0; i < Math.max(0, extra); i++) {
    chars.push(charset[randomInt(charset.length)]);
  }

  // Fisher-Yates shuffle
  for (let i = chars.length - 1; i > 0; i--) {
    const j = randomInt(i + 1);
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }

  return chars.join('');
}

// ── Passphrase generation ─────────────────────────────────────────────────
function generatePassphrase() {
  const count = parseInt(document.getElementById('word-count').value, 10);
  const words = [];
  for (let i = 0; i < count; i++) {
    words.push(EFF_WORDS[randomInt(EFF_WORDS.length)]);
  }
  // Add a number suffix for entropy
  const num = randomInt(9000) + 1000;
  words.push(String(num));
  return words.join('-');
}

// ── Strength scoring ──────────────────────────────────────────────────────
function scorePassword(pw) {
  if (!pw) return { score: 0, label: '—', color: '#2a2d35' };
  let score = 0;
  if (pw.length >= 8)  score++;
  if (pw.length >= 14) score++;
  if (pw.length >= 20) score++;
  if (/[A-Z]/.test(pw) && /[a-z]/.test(pw)) score++;
  if (/[0-9]/.test(pw)) score++;
  if (/[^A-Za-z0-9]/.test(pw)) score++;
  // cap at 5
  score = Math.min(5, score);
  const labels = ['Very weak', 'Weak', 'Fair', 'Good', 'Strong', 'Very strong'];
  const colors = ['#c05040', '#c07830', '#c08030', '#7090c0', '#5a6af0', '#4a9d6f'];
  return { score, label: labels[score], color: colors[score] };
}

function updateStrengthBar(pw) {
  const { score, label, color } = scorePassword(pw);
  for (let i = 0; i < 5; i++) {
    document.getElementById('seg' + i).style.background = i < score ? color : '#2a2d35';
  }
  document.getElementById('strength-label').textContent = label;
  document.getElementById('strength-label').style.color = color;
}

// ── Regen ─────────────────────────────────────────────────────────────────
function regen() {
  if (state.mode === 'password') {
    const pw = generatePassword();
    state.credential = pw;
    document.getElementById('password-display').textContent = pw;
    updateStrengthBar(pw);
    document.getElementById('copy-pw-btn').textContent = 'Copy';
    document.getElementById('copy-pw-btn').classList.remove('copied');
  } else {
    const pp = generatePassphrase();
    state.credential = pp;
    document.getElementById('passphrase-display').textContent = pp;
    document.getElementById('copy-pp-btn').textContent = 'Copy';
    document.getElementById('copy-pp-btn').classList.remove('copied');
  }
}

function onLengthChange() {
  const v = document.getElementById('pw-length').value;
  document.getElementById('pw-length-val').textContent = `${v} chars`;
  regen();
}

function onWordCountChange() {
  const v = document.getElementById('word-count').value;
  document.getElementById('word-count-val').textContent = `${v} words`;
  regen();
}

// ── Copy credential ───────────────────────────────────────────────────────
async function copyCredential() {
  if (!state.credential) return;
  try {
    await navigator.clipboard.writeText(state.credential);
    const id = state.mode === 'password' ? 'copy-pw-btn' : 'copy-pp-btn';
    const btn = document.getElementById(id);
    btn.textContent = 'Copied!';
    btn.classList.add('copied');
    setTimeout(() => {
      btn.textContent = 'Copy';
      btn.classList.remove('copied');
    }, 2000);
  } catch {}
}

// ── Save credential locally ───────────────────────────────────────────────
async function saveCredentialLocally() {
  if (!state.credential) return;
  const ts = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
  await window.starkive.saveCredential(state.credential, ts);
}

// ── Source & output selection ─────────────────────────────────────────────
async function selectSource() {
  const p = await window.starkive.selectFile();
  if (!p) return;
  state.sourcePath = p;
  const display = document.getElementById('source-display');
  display.textContent = p;
  display.classList.add('has-path');
}

async function selectOutput() {
  const p = await window.starkive.selectOutputPath();
  if (!p) return;
  state.outputPath = p;
  const display = document.getElementById('output-display');
  display.textContent = p;
  display.classList.add('has-path');
}

// ── Zip handler ───────────────────────────────────────────────────────────
async function doZip() {
  if (!state.sourcePath) { alert('Please select a source file or folder.'); return; }
  if (!state.outputPath) { alert('Please set an output path.'); return; }
  if (!state.credential) { alert('No credential generated yet.'); return; }

  const btn = document.getElementById('zip-btn');
  btn.disabled = true;
  btn.textContent = 'Creating archive…';

  try {
    const result = await window.starkive.zipEncrypt(state.sourcePath, state.outputPath, state.credential);

    if (!result.success) {
      btn.textContent = 'Error — try again';
      btn.classList.add('error');
      setTimeout(() => { btn.disabled = false; btn.textContent = 'Create Encrypted Archive'; btn.classList.remove('error'); }, 3000);
      alert('Archive failed: ' + (result.error || 'Unknown error'));
      return;
    }

    // Audit log (if enabled and not exhausted)
    let auditLogged = false;
    if (state.auditOn && state.user) {
      const auditState = getAuditState(state.user, true);
      if (auditState === 'free-active' || auditState === 'pro-active') {
        const label = document.getElementById('f-label').value.trim();
        if (label) {
          try {
            const archiveName = state.outputPath.split(/[\\/]/).pop();
            await api('POST', '/audit/events', {
              event_label: label,
              created_by:  document.getElementById('f-created-by').value.trim() || undefined,
              category:    document.getElementById('f-category').value || undefined,
              retention_policy: document.getElementById('f-retention').value || undefined,
              notes:       document.getElementById('f-notes').value.trim() || undefined,
              archive_name: archiveName,
              file_size_bytes: result.fileSizeBytes || 0,
              password_mode: state.mode,
            });
            auditLogged = true;
            // Refresh user data to update usage counts
            try {
              const me = await api('GET', '/auth/me');
              state.user = me.user;
              renderAuditState();
            } catch {}
          } catch (e) {
            console.warn('Audit log failed:', e.message);
          }
        }
      }
    }

    // Show success
    showSuccess(result, auditLogged);

    btn.disabled = false;
    btn.textContent = 'Create Encrypted Archive';

  } catch (err) {
    btn.disabled = false;
    btn.textContent = 'Create Encrypted Archive';
    alert('Unexpected error: ' + err.message);
  }
}

// ── Success modal ─────────────────────────────────────────────────────────
function showSuccess(result, auditLogged) {
  const archiveName = (state.outputPath || '').split(/[\\/]/).pop() || state.outputPath;
  const sizeStr = result.fileSizeBytes
    ? formatBytes(result.fileSizeBytes)
    : 'Unknown';

  document.getElementById('success-output').textContent = archiveName;
  document.getElementById('success-size').textContent = sizeStr;

  const auditRow = document.getElementById('success-audit-row');
  if (auditLogged) {
    auditRow.style.display = 'flex';
    document.getElementById('success-audit-label').textContent = 'Logged ✓';
  } else {
    auditRow.style.display = 'none';
  }

  document.getElementById('success-overlay').classList.add('visible');
}

function hideSuccess() {
  document.getElementById('success-overlay').classList.remove('visible');
}

function closeSuccessOnOverlay(e) {
  if (e.target === document.getElementById('success-overlay')) hideSuccess();
}

function formatBytes(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
}

// ── Sign-up modal ─────────────────────────────────────────────────────────
function showSignUpModal() {
  document.getElementById('modal-overlay').classList.add('visible');
  document.getElementById('m-email').value = '';
  document.getElementById('m-password').value = '';
}

function hideSignUpModal() {
  document.getElementById('modal-overlay').classList.remove('visible');
}

function closeModalOnOverlay(e) {
  if (e.target === document.getElementById('modal-overlay')) hideSignUpModal();
}

async function googleSignIn() {
  hideSignUpModal();
  await window.starkive.openGoogleAuth();
  // Token arrives via deep link → onAuthToken handler
}

async function emailAuth() {
  const email    = document.getElementById('m-email').value.trim();
  const password = document.getElementById('m-password').value;
  if (!email || !password) { alert('Email and password required.'); return; }
  if (password.length < 8) { alert('Password must be at least 8 characters.'); return; }

  try {
    // Try login first, fall back to signup
    let data;
    try {
      data = await api('POST', '/auth/login', { email, password });
    } catch (e) {
      if (e.status === 401) {
        data = await api('POST', '/auth/signup', { email, password });
      } else {
        throw e;
      }
    }
    state.authToken = data.token;
    setUser(data.user);
    hideSignUpModal();
  } catch (err) {
    alert('Auth error: ' + (err.message || 'Please try again.'));
  }
}

// ── Init ──────────────────────────────────────────────────────────────────
(function init() {
  regen();
  renderAuditState();
})();

// ── Expose to HTML onclick handlers ──────────────────────────────────────
const app = {
  setMode,
  regen,
  onLengthChange,
  onWordCountChange,
  copyCredential,
  saveCredentialLocally,
  selectSource,
  selectOutput,
  doZip,
  showSignUpModal,
  hideSignUpModal,
  closeModalOnOverlay,
  googleSignIn,
  emailAuth,
  hideSuccess,
  closeSuccessOnOverlay,
  onAuditToggle,
  dismissExhausted,
  goToAuditHistory,
  openUpgrade,
  handleAccountChipClick,
};
