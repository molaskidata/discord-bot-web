import express from "express";
import dotenv from "dotenv";
import fetch from "node-fetch";
dotenv.config({ path: "../.env" });

const app = express();
const port = 3002;

// Security: Limit request body size to prevent DoS attacks
app.use(express.json({ limit: '100kb' }));

app.get('/', (req, res) => {
  res.json({
    status: 'Coffee & Codes Activity Server Online',
    version: '1.0.0',
    uptime: process.uptime(),
    timestamp: new Date().toISOString()
  });
});

app.post("/api/token", async (req, res) => {
  try {
    const { code } = req.body;
    
    // Input validation - prevent DoS and injection attacks
    if (!code || typeof code !== 'string') {
      return res.status(400).json({ error: 'Invalid request: code required' });
    }
    
    // Discord OAuth codes are typically 30 chars, alphanumeric with dashes/underscores
    // Reject suspicious inputs
    if (code.length > 100 || code.length < 10) {
      return res.status(400).json({ error: 'Invalid code format: length' });
    }
    
    if (!/^[a-zA-Z0-9_-]+$/.test(code)) {
      return res.status(400).json({ error: 'Invalid code format: characters' });
    }
    
    const response = await fetch(`https://discord.com/api/oauth2/token`, {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
      body: new URLSearchParams({
        client_id: process.env.VITE_DISCORD_CLIENT_ID,
        client_secret: process.env.DISCORD_CLIENT_SECRET,
        grant_type: "authorization_code",
        code: code,
      }),
    });

    const data = await response.json();
    
    if (!response.ok) {
      // Don't expose Discord's error details to prevent info leakage
      return res.status(401).json({ error: 'Authentication failed' });
    }
    
    const { access_token } = data;
    res.send({access_token});
  } catch (error) {
    // Don't log full error object - may contain sensitive data
    console.error('Discord OAuth error:', error.message);
    res.status(500).json({ error: 'OAuth authentication failed' });
  }
});

app.listen(port, '0.0.0.0', () => {
  console.log(`☕ Coffee & Codes Activity Server listening at http://localhost:${port}`);
});
