// Channel and guild IDs for pinging (angepasst auf neuen Server/Channel)
const PING_GUILD_ID = '1415044198792691858'; // Neuer Server ID
const PING_CHANNEL_ID = '1440998057016557619'; // Neuer Channel ID

// Main bot sends !pingme every hour
function sendPingToPingBot() {
    const guild = client.guilds.cache.get(PING_GUILD_ID);
    if (guild) {
        const channel = guild.channels.cache.get(PING_CHANNEL_ID);
        if (channel) {
            channel.send('!pingme');
        }
    }
}

setInterval(sendPingToPingBot, 60 * 60 * 1000); // every hour

// Assign GitHub-Coder role to user after linking
async function assignGithubCoderRole(discordId) {
    try {
        // Use the first guild or specify your guild ID
        const guild = client.guilds.cache.first();
        if (!guild) return;
        const member = await guild.members.fetch(discordId);
        if (!member) return;
        await member.roles.add('1440681068708630621');
    } catch (err) {
        console.error('Error assigning GitHub-Coder role:', err);
    }
}

require('dotenv').config();
const { Client, GatewayIntentBits, ActivityType } = require('discord.js');
const express = require('express');
const querystring = require('querystring');
const fs = require('fs');
const axios = require('axios');

const BOT_INFO = {
    name: "CoderMaster",
    version: "1.0.0",
    author: "ozzygirl/mungabee"
    // Note: publicKey removed for security - store in .env if needed
};

const client = new Client({
    intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMessages,
        GatewayIntentBits.MessageContent
    ]
});

// Make client globally accessible for commands.js
global.client = client;

let gameTimer = 0;
const MAX_HOURS = 20;

const app = express();
const PORT = process.env.PORT || 3000;
// GitHub OAuth login endpoint
app.get('/github/login', (req, res) => {
    const discordId = req.query.discordId;
    const params = querystring.stringify({
        client_id: process.env.GITHUB_CLIENT_ID,
        redirect_uri: 'http://localhost:3000/github/callback',
        scope: 'read:user repo',
        state: discordId // Use Discord user ID as state
    });
    res.redirect(`https://github.com/login/oauth/authorize?${params}`);
});

// GitHub OAuth callback endpoint
app.get('/github/callback', async (req, res) => {
    const code = req.query.code;
    const discordId = req.query.state;
    try {
        // Exchange code for access token
        const tokenRes = await axios.post(
            'https://github.com/login/oauth/access_token',
            {
                client_id: process.env.GITHUB_CLIENT_ID,
                client_secret: process.env.GITHUB_CLIENT_SECRET,
                code,
                redirect_uri: 'http://localhost:3000/github/callback'
            },
            { Accept: 'application/json' }
        );
        const accessToken = tokenRes.data.access_token;

        // Get GitHub username
        const userRes = await axios.get('https://api.github.com/user', null, {
            Authorization: `token ${accessToken}`
        });
        const githubUsername = userRes.data.login;

        // Save mapping to JSON file
        let data = {};
        if (fs.existsSync('github_links.json')) {
            data = JSON.parse(fs.readFileSync('github_links.json'));
        }
        data[discordId] = { githubUsername, accessToken };
        fs.writeFileSync('github_links.json', JSON.stringify(data, null, 2));

        // Assign role after linking
        await assignGithubCoderRole(discordId);

        res.send('GitHub account linked! You can close this window and return to Discord.');
    } catch (err) {
        res.status(500).send('GitHub authentication failed.');
    }
});

// Security: Limit request body size to prevent DoS attacks
app.use(express.json({ limit: '100kb' }));

// Helper function to check if request is from localhost
const isLocalhost = (req) => {
    const ip = req.ip || req.connection.remoteAddress;
    return ip === '127.0.0.1' || ip === '::1' || ip === '::ffff:127.0.0.1';
};

app.get('/', (req, res) => {
    // Security: Only expose detailed info to localhost
    if (!isLocalhost(req)) {
        return res.status(200).json({ status: 'OK' });
    }
    
    res.json({
        status: 'Bot Online',
        uptime: process.uptime(),
        timestamp: new Date().toISOString(),
        bot: BOT_INFO
    });
});

app.get('/health', (req, res) => {
    res.status(200).send('OK');
});

const server = app.listen(PORT, () => {
    console.log(`✅ HTTP Server running on port ${PORT}`);
    console.log(`🌍 Accessible at http://localhost:${PORT}`);
});


// Import command handler from commands.js
const { handleCommand } = require('./commands');

client.once('ready', () => {
    console.log(`${BOT_INFO.name} v${BOT_INFO.version} is online!`);
    console.log(`Logged in as ${client.user.tag}`);
    
    updateGameStatus();
    setInterval(updateGameStatus, 3600000);
});

function updateGameStatus() {
    gameTimer++;
    if (gameTimer > MAX_HOURS) {
        gameTimer = 2;
    }
    
    client.user.setPresence({
        activities: [{
            name: 'Dead by Daylight',
            type: ActivityType.Playing,
            details: `${gameTimer}h gespielt`,
            state: `Playing Killer for ${gameTimer} hours`,
            applicationId: '1435244593301159978',
            assets: {
                large_image: 'deadbydaylight',
                large_text: 'Dead by Daylight'
            },
            timestamps: {
                start: Date.now() - (gameTimer * 3600000)
            }
        }],
        status: 'online'
    });
}

const processedMessages = new Set();

client.on('messageCreate', (message) => {
    if (message.author.bot) return;

    const messageId = message.id;
    if (processedMessages.has(messageId)) return;
    processedMessages.add(messageId);

    setTimeout(() => {
        processedMessages.delete(messageId);
    }, 60000);

    // Use the new command handler
    handleCommand(message, BOT_INFO);
});

client.login(process.env.DISCORD_TOKEN);

setInterval(() => {
    console.log(`Bot alive: ${new Date().toISOString()}`);
    process.stdout.write('\x1b[0G');
}, 60000);

setInterval(() => {
    console.log(`Bot alive at: ${new Date().toISOString()}`);
}, 300000);

module.exports = { client, BOT_INFO, app };