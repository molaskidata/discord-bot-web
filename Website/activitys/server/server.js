import express from "express";
import dotenv from "dotenv";
import fetch from "node-fetch";
import cors from "cors";
dotenv.config({ path: "../.env" });

const app = express();
const port = 3002;

app.use(express.json({ limit: '100kb' }));

// enable CORS for OAuth redirects from the website
app.use(cors());

// GitHub OAuth linkage storage (in-memory; consider persisting)
const userLinks = new Map();

const GITHUB_CLIENT_ID = process.env.GITHUB_CLIENT_ID;
const GITHUB_CLIENT_SECRET = process.env.GITHUB_CLIENT_SECRET;
const CALLBACK_URL = process.env.GITHUB_CALLBACK_URL || 'http://localhost:3002/github/callback';

app.get('/github/auth', (req, res) => {
  const discordId = req.query.discordId;
  if (!discordId) return res.status(400).json({ error: 'Discord ID required' });
  const githubAuthUrl = `https://github.com/login/oauth/authorize?client_id=${GITHUB_CLIENT_ID}&redirect_uri=${encodeURIComponent(CALLBACK_URL)}&scope=user:email,read:user&state=${discordId}`;
  res.redirect(githubAuthUrl);
});

app.get('/github/callback', async (req, res) => {
  const { code, state: discordId } = req.query;
  if (!code || !discordId) return res.status(400).send('Authentication failed: Missing code or Discord ID');
  try {
    const tokenResponse = await fetch('https://github.com/login/oauth/access_token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
      body: JSON.stringify({ client_id: GITHUB_CLIENT_ID, client_secret: GITHUB_CLIENT_SECRET, code: code, redirect_uri: CALLBACK_URL })
    });
    const tokenData = await tokenResponse.json();
    if (tokenData.error) throw new Error(tokenData.error_description || 'Failed to get access token');
    const accessToken = tokenData.access_token;
    const userResponse = await fetch('https://api.github.com/user', { headers: { 'Authorization': `Bearer ${accessToken}`, 'Accept': 'application/vnd.github.v3+json' } });
    const githubUser = await userResponse.json();
    userLinks.set(discordId, { githubId: githubUser.id, githubUsername: githubUser.login, githubAccessToken: accessToken, linkedAt: new Date().toISOString() });
    console.log(`✅ Linked Discord ID ${discordId} to GitHub @${githubUser.login}`);
    res.send(`<html><body><h2>GitHub account @${githubUser.login} linked to Discord ID ${discordId}.</h2><p>You can close this window.</p></body></html>`);
  } catch (error) {
    console.error('OAuth error:', error);
    res.status(500).send('OAuth Error');
  }
});

app.get('/api/github/stats/:discordId', async (req, res) => {
  const { discordId } = req.params;
  const userLink = userLinks.get(discordId);
  if (!userLink) return res.status(404).json({ error: 'User not linked' });
  try {
    const userResponse = await fetch(`https://api.github.com/users/${userLink.githubUsername}`, { headers: { 'Authorization': `Bearer ${userLink.githubAccessToken}`, 'Accept': 'application/vnd.github.v3+json' } });
    const userData = await userResponse.json();
    const eventsResponse = await fetch(`https://api.github.com/users/${userLink.githubUsername}/events`, { headers: { 'Authorization': `Bearer ${userLink.githubAccessToken}`, 'Accept': 'application/vnd.github.v3+json' } });
    const events = await eventsResponse.json();
    const pushEvents = Array.isArray(events) ? events.filter(e => e.type === 'PushEvent') : [];
    const totalCommits = pushEvents.reduce((sum, event) => sum + (event.payload?.commits?.length || 0), 0);
    res.json({ discordId, github: { username: userLink.githubUsername, publicRepos: userData.public_repos, followers: userData.followers, following: userData.following, recentCommits: totalCommits, accountCreated: userData.created_at }, linkedAt: userLink.linkedAt });
  } catch (error) {
    console.error('Error fetching GitHub stats:', error);
    res.status(500).json({ error: 'Failed to fetch GitHub stats' });
  }
});

app.get('/api/github/check/:discordId', (req, res) => {
  const { discordId } = req.params;
  const isLinked = userLinks.has(discordId);
  res.json({ discordId, linked: isLinked, githubUsername: isLinked ? userLinks.get(discordId).githubUsername : null });
});

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
    
    if (!code || typeof code !== 'string') {
      return res.status(400).json({ error: 'Invalid request: code required' });
    }
    
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
      return res.status(401).json({ error: 'Authentication failed' });
    }
    
    const { access_token } = data;
    res.send({access_token});
  } catch (error) {
    console.error('Discord OAuth error:', error.message);
    res.status(500).json({ error: 'OAuth authentication failed' });
  }
});

app.listen(port, '0.0.0.0', () => {
  console.log(`☕ Coffee & Codes Activity Server listening at http://localhost:${port}`);
});
