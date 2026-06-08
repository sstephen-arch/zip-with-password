# Starkive Backend API

Express + Supabase REST API for the Starkive desktop app.

## Stack

- **Node.js** 18+ / **Express** 4
- **Supabase** (Postgres) — users + audit events
- **JWT** (7-day expiry) — stateless auth
- **Google OAuth 2.0** — via `passport-google-oauth20`
- **bcryptjs** — password hashing (12 rounds)

## Setup

### 1. Clone and install

```bash
cd starkive-backend
npm install
```

### 2. Supabase

1. Create a project at https://supabase.com
2. Run `db/schema.sql` in the Supabase SQL editor
3. Copy your **Project URL**, **anon key**, and **service role key**

### 3. Google OAuth

1. Go to https://console.cloud.google.com → APIs & Services → Credentials
2. Create an **OAuth 2.0 Client ID** (Web application)
3. Authorized redirect URI: `http://localhost:3000/auth/google/callback` (dev)
4. Copy Client ID and Client Secret

### 4. Environment

```bash
cp .env.example .env
# Edit .env with your values
```

Required vars:

| Variable | Description |
|---|---|
| `SUPABASE_URL` | Your Supabase project URL |
| `SUPABASE_ANON_KEY` | Supabase anon/public key |
| `SUPABASE_SERVICE_ROLE_KEY` | Supabase service role key (server-side only) |
| `JWT_SECRET` | Long random string (32+ chars) |
| `JWT_EXPIRES_IN` | Token expiry, e.g. `7d` |
| `GOOGLE_CLIENT_ID` | From Google Cloud Console |
| `GOOGLE_CLIENT_SECRET` | From Google Cloud Console |
| `GOOGLE_CALLBACK_URL` | OAuth callback, e.g. `http://localhost:3000/auth/google/callback` |
| `PORT` | Default: `3000` |
| `NODE_ENV` | `development` or `production` |
| `MS_STORE_PRODUCT_ID` | Microsoft Store product ID (for entitlement) |

### 5. Run

```bash
# Development
node index.js

# Or with nodemon
npx nodemon index.js
```

Health check: `GET http://localhost:3000/health` → `{"status":"ok"}`

---

## API Reference

### Auth

| Method | Path | Body | Description |
|--------|------|------|-------------|
| POST | `/auth/signup` | `{email, password}` | Create account, returns `{token, user}` |
| POST | `/auth/login` | `{email, password}` | Sign in, returns `{token, user}` |
| GET | `/auth/google` | — | Redirect to Google OAuth |
| GET | `/auth/google/callback` | — | OAuth callback → redirects to `starkive://auth?token=JWT` |
| GET | `/auth/me` | — (JWT required) | Returns current user |
| POST | `/auth/logout` | — (JWT required) | Invalidates session (client-side token drop) |

### Audit Events

All routes require `Authorization: Bearer <JWT>`.

| Method | Path | Description |
|--------|------|-------------|
| POST | `/audit/events` | Log a new audit event |
| GET | `/audit/events` | List events (paginated, filterable) |
| GET | `/audit/export` | Download CSV of all events |
| DELETE | `/audit/events/:id` | Delete own event |

#### POST `/audit/events` body

```json
{
  "event_label": "Q2 2026 HR Archive",
  "created_by": "Shem Stephen",
  "category": "HR",
  "retention_policy": "7y",
  "notes": "Quarterly HR files",
  "archive_name": "hr-q2-2026.zip",
  "file_size_bytes": 4194304,
  "password_mode": "password"
}
```

#### GET `/audit/events` query params

| Param | Example | Description |
|-------|---------|-------------|
| `page` | `1` | Page number (default 1) |
| `limit` | `20` | Results per page (default 20, max 100) |
| `category` | `HR` | Filter by category |
| `from` | `2026-01-01` | Filter from date |
| `to` | `2026-06-30` | Filter to date |

---

## Plan Limits

| | Free | Pro |
|---|---|---|
| Audit events | 5 total | Unlimited |
| File size logged | 2 MB | Unlimited |
| CSV export | ✗ | ✓ |
| Retention policies | ✗ | ✓ |
| Price | Free | $1/month (Microsoft Store) |

---

## Database Schema

See `db/schema.sql`. Two tables:

- **users** — id, email, password_hash, google_id, display_name, plan, audit_events_used, audit_bytes_used
- **audit_events** — id, user_id, event_label, created_by, category, retention_policy, notes, archive_name, file_size_bytes, password_mode, created_at

---

## Security Notes

- Passwords hashed with bcrypt (12 rounds)
- JWTs signed with `JWT_SECRET` — rotate this if compromised
- `SUPABASE_SERVICE_ROLE_KEY` is server-only — never expose to client
- Rate limited: 100 req / 15 min per IP
- Helmet.js sets secure HTTP headers
- CORS restricted to `starkive://` and `localhost`

---

## Deployment

Tested with **Fly.io** and **Railway**. Any Node.js host works.

```bash
# Fly.io example
fly launch --name starkive-api
fly secrets set SUPABASE_URL=... JWT_SECRET=... GOOGLE_CLIENT_ID=... # etc.
fly deploy
```

After deploying, update:
- `GOOGLE_CALLBACK_URL` → `https://your-domain.fly.dev/auth/google/callback`
- Google Cloud Console → OAuth client → add the production redirect URI
- `starkive-app/main.js` → update `http://localhost:3000` to production URL
