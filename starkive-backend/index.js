require('dotenv').config();
require('express-async-errors');
const express = require('express');
const cors = require('cors');
const helmet = require('helmet');
const rateLimit = require('express-rate-limit');
const passport = require('passport');

const authRoutes = require('./routes/auth');
const auditRoutes = require('./routes/audit');

const app = express();

app.use(helmet());
app.use(cors({ origin: ['starkive://.', 'http://localhost'] }));
app.use(express.json());
app.use(passport.initialize());

const limiter = rateLimit({ windowMs: 15 * 60 * 1000, max: 100 });
app.use(limiter);

app.use('/auth', authRoutes);
app.use('/audit', auditRoutes);

app.get('/health', (req, res) => res.json({ status: 'ok' }));

// Global error handler
app.use((err, req, res, next) => {
  console.error(err);
  res.status(err.status || 500).json({ error: err.message || 'Internal server error' });
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => console.log(`Starkive API running on port ${PORT}`));
