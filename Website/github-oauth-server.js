import express from "express";
import dotenv from "dotenv";
import fetch from "node-fetch";
import cors from "cors";

dotenv.config();

const app = express();
const PORT = process.env.GITHUB_OAUTH_PORT || 3000;

app.use(cors());
app.use(express.json());

// Store GitHub-Discord links (in production, use a database)
const userLinks = new Map();

// GitHub OAuth configuration
const GITHUB_CLIENT_ID = process.env.GITHUB_CLIENT_ID;
const GITHUB_CLIENT_SECRET = process.env.GITHUB_CLIENT_SECRET;
const CALLBACK_URL = process.env.GITHUB_CALLBACK_URL || 'http://localhost:3000/github/callback';

// Health check
app.get('/', (req, res) => {
  res.json({
    status: 'GitHub-Discord OAuth Server Online',
    version: '1.0.0',
    uptime: process.uptime(),
    timestamp: new Date().toISOString()
  });
});

// Step 1: Redirect to GitHub OAuth
app.get('/github/auth', (req, res) => {
  const discordId = req.query.discordId;
  
  if (!discordId) {
    return res.status(400).json({ error: 'Discord ID required' });
  }
  
  const githubAuthUrl = `https://github.com/login/oauth/authorize?` +
    `client_id=${GITHUB_CLIENT_ID}&` +
    `redirect_uri=${encodeURIComponent(CALLBACK_URL)}&` +
    `scope=user:email,read:user&` +
    `state=${discordId}`;
  
  res.redirect(githubAuthUrl);
});

// Step 2: Handle GitHub OAuth callback
app.get('/github/callback', async (req, res) => {
  const { code, state: discordId } = req.query;
  
  if (!code || !discordId) {
    return res.status(400).send('Authentication failed: Missing code or Discord ID');
  }
  
  try {
    // Exchange code for access token
    const tokenResponse = await fetch('https://github.com/login/oauth/access_token', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      body: JSON.stringify({
        client_id: GITHUB_CLIENT_ID,
        client_secret: GITHUB_CLIENT_SECRET,
        code: code,
        redirect_uri: CALLBACK_URL
      })
    });
    
    const tokenData = await tokenResponse.json();
    
    if (tokenData.error) {
      throw new Error(tokenData.error_description || 'Failed to get access token');
    }
    
    const accessToken = tokenData.access_token;
    
    // Get GitHub user info
    const userResponse = await fetch('https://api.github.com/user', {
      headers: {
        'Authorization': `Bearer ${accessToken}`,
        'Accept': 'application/vnd.github.v3+json'
      }
    });
    
    const githubUser = await userResponse.json();
    
    // Store the link (in production, save to database)
    userLinks.set(discordId, {
      githubId: githubUser.id,
      githubUsername: githubUser.login,
      githubAccessToken: accessToken,
      linkedAt: new Date().toISOString()
    });
    
    console.log(`✅ Linked Discord ID ${discordId} to GitHub @${githubUser.login}`);
    
    // Redirect back to success page
    res.send(`
      <!DOCTYPE html>
      <html>
      <head>
        <title>GitHub Connected!</title>
        <style>
          body {
            font-family: 'Arial', sans-serif;
            background: linear-gradient(135deg, #1a2332, #2c3e50);
            color: white;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
          }
          .success-box {
            background: rgba(255, 255, 255, 0.1);
            border: 2px solid #4a90e2;
            border-radius: 20px;
            padding: 40px;
            text-align: center;
            max-width: 500px;
          }
          h1 { color: #74c0fc; }
          .github-info {
            background: rgba(255, 255, 255, 0.05);
            padding: 20px;
            border-radius: 10px;
            margin: 20px 0;
          }
          a {
            display: inline-block;
            background: linear-gradient(145deg, #245785, #4a90e2);
            color: white;
            padding: 12px 30px;
            border-radius: 20px;
            text-decoration: none;
            margin-top: 20px;
            transition: all 0.3s ease;
          }
          a:hover {
            transform: scale(1.05);
            background: linear-gradient(145deg, #4a90e2, #74c0fc);
          }
        </style>
      </head>
      <body>
        <div class="success-box">
          <h1>🎉 Successfully Connected!</h1>
          <div class="github-info">
            <p><strong>GitHub Account:</strong> @${githubUser.login}</p>
            <p><strong>Discord ID:</strong> ${discordId}</p>
          </div>
          <p>Your GitHub account is now linked to your Discord profile!<br>
          You'll start earning XP based on your GitHub activity.</p>
          <a href="https://thecoffeylounge.com">Return to Coffee & Codes</a>
        </div>
      </body>
      </html>
    `);
    
  } catch (error) {
    console.error('❌ OAuth Error:', error);
    res.status(500).send(`
      <!DOCTYPE html>
      <html>
      <head>
        <title>Connection Failed</title>
        <style>
          body {
            font-family: 'Arial', sans-serif;
            background: linear-gradient(135deg, #1a2332, #2c3e50);
            color: white;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
          }
          .error-box {
            background: rgba(255, 0, 0, 0.1);
            border: 2px solid #e74c3c;
            border-radius: 20px;
            padding: 40px;
            text-align: center;
            max-width: 500px;
          }
        </style>
      </head>
      <body>
        <div class="error-box">
          <h1>❌ Connection Failed</h1>
          <p>${error.message}</p>
          <a href="https://thecoffeylounge.com/github-connect" style="display: inline-block; background: #e74c3c; color: white; padding: 12px 30px; border-radius: 20px; text-decoration: none; margin-top: 20px;">Try Again</a>
        </div>
      </body>
      </html>
    `);
  }
});

// API endpoint to get GitHub stats for a Discord user
app.get('/api/github/stats/:discordId', async (req, res) => {
  const { discordId } = req.params;
  const userLink = userLinks.get(discordId);
  
  if (!userLink) {
    return res.status(404).json({ error: 'User not linked' });
  }
  
  try {
    // Get user's GitHub stats
    const userResponse = await fetch(`https://api.github.com/users/${userLink.githubUsername}`, {
      headers: {
        'Authorization': `Bearer ${userLink.githubAccessToken}`,
        'Accept': 'application/vnd.github.v3+json'
      }
    });
    
    const userData = await userResponse.json();
    
    // Get recent commits (across all repos)
    const eventsResponse = await fetch(`https://api.github.com/users/${userLink.githubUsername}/events`, {
      headers: {
        'Authorization': `Bearer ${userLink.githubAccessToken}`,
        'Accept': 'application/vnd.github.v3+json'
      }
    });
    
    const events = await eventsResponse.json();
    const pushEvents = events.filter(e => e.type === 'PushEvent');
    const totalCommits = pushEvents.reduce((sum, event) => sum + (event.payload.commits?.length || 0), 0);
    
    res.json({
      discordId,
      github: {
        username: userLink.githubUsername,
        publicRepos: userData.public_repos,
        followers: userData.followers,
        following: userData.following,
        recentCommits: totalCommits,
        accountCreated: userData.created_at
      },
      linkedAt: userLink.linkedAt
    });
    
  } catch (error) {
    console.error('Error fetching GitHub stats:', error);
    res.status(500).json({ error: 'Failed to fetch GitHub stats' });
  }
});

// API endpoint to check if user is linked
app.get('/api/github/check/:discordId', (req, res) => {
  const { discordId } = req.params;
  const isLinked = userLinks.has(discordId);
  
  res.json({
    discordId,
    linked: isLinked,
    githubUsername: isLinked ? userLinks.get(discordId).githubUsername : null
  });
});

app.listen(PORT, () => {
  console.log(`🚀 GitHub-Discord OAuth Server running on http://localhost:${PORT}`);
  console.log(`📝 Callback URL: ${CALLBACK_URL}`);
  console.log(`🔗 Auth endpoint: http://localhost:${PORT}/github/auth?discordId=YOUR_DISCORD_ID`);
});
