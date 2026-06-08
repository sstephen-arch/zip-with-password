const express = require('express');
const router = express.Router();
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const passport = require('passport');
const { Strategy: GoogleStrategy } = require('passport-google-oauth20');
const supabase = require('../db/supabase');
const requireAuth = require('../middleware/requireAuth');

// ── JWT helper ─────────────────────────────────────────────────────────────
function signToken(user) {
  return jwt.sign(
    { userId: user.id, email: user.email, plan: user.plan },
    process.env.JWT_SECRET,
    { expiresIn: process.env.JWT_EXPIRES_IN || '7d' }
  );
}

function safeUser(user) {
  const { password_hash, ...rest } = user;
  return rest;
}

// ── Google OAuth strategy ──────────────────────────────────────────────────
passport.use(new GoogleStrategy(
  {
    clientID: process.env.GOOGLE_CLIENT_ID,
    clientSecret: process.env.GOOGLE_CLIENT_SECRET,
    callbackURL: process.env.GOOGLE_CALLBACK_URL,
  },
  async (accessToken, refreshToken, profile, done) => {
    try {
      const email = profile.emails[0].value;
      const googleId = profile.id;
      const displayName = profile.displayName;

      // Try to find by google_id first
      let { data: user } = await supabase
        .from('users')
        .select('*')
        .eq('google_id', googleId)
        .single();

      if (!user) {
        // Try to find by email (existing email account)
        const { data: emailUser } = await supabase
          .from('users')
          .select('*')
          .eq('email', email)
          .single();

        if (emailUser) {
          // Link google_id to existing account
          const { data: updated } = await supabase
            .from('users')
            .update({ google_id: googleId, display_name: displayName })
            .eq('id', emailUser.id)
            .select()
            .single();
          user = updated;
        } else {
          // Create new user
          const { data: newUser, error } = await supabase
            .from('users')
            .insert({ email, google_id: googleId, display_name: displayName })
            .select()
            .single();
          if (error) return done(error);
          user = newUser;
        }
      }

      return done(null, user);
    } catch (err) {
      return done(err);
    }
  }
));

// ── POST /auth/signup ──────────────────────────────────────────────────────
router.post('/signup', async (req, res) => {
  const { email, password } = req.body;
  if (!email || !password) {
    return res.status(400).json({ error: 'Email and password are required' });
  }

  // Check existing
  const { data: existing } = await supabase
    .from('users')
    .select('id')
    .eq('email', email)
    .single();

  if (existing) {
    return res.status(409).json({ error: 'Email already registered' });
  }

  const password_hash = await bcrypt.hash(password, 12);

  const { data: user, error } = await supabase
    .from('users')
    .insert({ email, password_hash })
    .select()
    .single();

  if (error) return res.status(500).json({ error: error.message });

  const token = signToken(user);
  res.status(201).json({ token, user: safeUser(user) });
});

// ── POST /auth/login ───────────────────────────────────────────────────────
router.post('/login', async (req, res) => {
  const { email, password } = req.body;
  if (!email || !password) {
    return res.status(400).json({ error: 'Email and password are required' });
  }

  const { data: user } = await supabase
    .from('users')
    .select('*')
    .eq('email', email)
    .single();

  if (!user || !user.password_hash) {
    return res.status(401).json({ error: 'Invalid credentials' });
  }

  const valid = await bcrypt.compare(password, user.password_hash);
  if (!valid) {
    return res.status(401).json({ error: 'Invalid credentials' });
  }

  const token = signToken(user);
  res.json({ token, user: safeUser(user) });
});

// ── GET /auth/google ───────────────────────────────────────────────────────
router.get('/google',
  passport.authenticate('google', { scope: ['email', 'profile'], session: false })
);

// ── GET /auth/google/callback ──────────────────────────────────────────────
router.get('/google/callback',
  passport.authenticate('google', { session: false, failureRedirect: '/auth/google/error' }),
  (req, res) => {
    const token = signToken(req.user);
    // Deep link back into the Electron app
    res.redirect(`starkive://auth?token=${token}`);
  }
);

router.get('/google/error', (req, res) => {
  res.status(401).json({ error: 'Google authentication failed' });
});

// ── GET /auth/me ───────────────────────────────────────────────────────────
router.get('/me', requireAuth, (req, res) => {
  res.json({ user: safeUser(req.user) });
});

// ── POST /auth/logout ──────────────────────────────────────────────────────
router.post('/logout', (req, res) => {
  // Stateless JWT — client clears token
  res.json({ message: 'Logged out' });
});

module.exports = router;
