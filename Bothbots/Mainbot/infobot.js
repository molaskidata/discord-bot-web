// Attach security moderation handler
const { handleSecurityModeration } = require('./commands');
const PING_GUILD_ID = '1415044198792691858';
const PING_CHANNEL_ID = '1448640396359106672';
function sendPingToPingBot() {
    const guild = client.guilds.cache.get(PING_GUILD_ID);
    if (guild) {
        const channel = guild.channels.cache.get(PING_CHANNEL_ID);
        if (channel) {
            channel.send('!pingme');
        }
    }
}

setInterval(sendPingToPingBot, 60 * 60 * 1000);
async function assignGithubCoderRole(discordId) {
    try {
        const guild = client.guilds.cache.first();
        if (!guild) return;
        const member = await guild.members.fetch(discordId);
        if (!member) return;
        await member.roles.add('1440681068708630621');
    } catch (err) {
        console.error('Error assigning GitHub-Coder role:', err);
    }
}

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const path = require('path');
require('dotenv').config({ path: path.join(__dirname, '.env') });
const { Client, GatewayIntentBits, ActivityType, PermissionsBitField } = require('discord.js');
const express = require('express');
const querystring = require('querystring');
const fs = require('fs');
const axios = require('axios');
const persistence = require('./persistence');

const BOT_INFO = {
    name: "CoderMaster",
    version: "1.0.0",
    author: "ozzygirl/mungabee"
};

const client = new Client({
    intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMessages,
        GatewayIntentBits.MessageContent,
        GatewayIntentBits.GuildVoiceStates
    ]
});

global.client = client;

// Attach security moderation handler after client is initialized
client.on('messageCreate', handleSecurityModeration);

let gameTimer = 0;
const MAX_HOURS = 20;

const app = express();
const PORT = process.env.PORT || 3000;
app.get('/github/login', (req, res) => {
    const discordId = req.query.discordId;
    const params = querystring.stringify({
        client_id: process.env.GITHUB_CLIENT_ID,
        redirect_uri: 'http://localhost:3000/github/callback',
        scope: 'read:user repo',
        state: discordId
    });
    res.redirect(`https://github.com/login/oauth/authorize?${params}`);
});

app.get('/github/callback', async (req, res) => {
    const code = req.query.code;
    const discordId = req.query.state;
    try {
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

        const userRes = await axios.get('https://api.github.com/user', null, {
            Authorization: `token ${accessToken}`
        });
        const githubUsername = userRes.data.login;

        let data = {};
        if (fs.existsSync('github_links.json')) {
            data = JSON.parse(fs.readFileSync('github_links.json'));
        }
        data[discordId] = { githubUsername, accessToken };
        fs.writeFileSync('github_links.json', JSON.stringify(data, null, 2));

        await assignGithubCoderRole(discordId);

        res.send('GitHub account linked! You can close this window and return to Discord.');
    } catch (err) {
        res.status(500).send('GitHub authentication failed.');
    }
});

app.use(express.json({ limit: '100kb' }));

const isLocalhost = (req) => {
    const ip = req.ip || req.connection.remoteAddress;
    return ip === '127.0.0.1' || ip === '::1' || ip === '::ffff:127.0.0.1';
};

app.get('/', (req, res) => {
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

// Public dashboard API for website (short, read-only)
app.get('/api/dashboard', (req, res) => {
    try {
        const botReady = !!(client && client.user);
        const presence = (client && client.presence && client.presence.status) ? client.presence.status : (botReady ? 'online' : 'offline');
        const guildCount = client && client.guilds ? client.guilds.cache.size : 0;
        const uptime = process.uptime();
        // Short, English command summary (kept concise for dashboard)
        const commands = [
            { cmd: '!info', desc: 'Bot basic info' },
            { cmd: '!ping', desc: 'Check bot latency' },
            { cmd: '!help', desc: 'Full command list' },
            { cmd: '!setsecuritymod', desc: 'Enable security (admin)' },
            { cmd: '!sban', desc: 'Ban a user (admin)' },
            { cmd: '!setupvoice', desc: 'Setup voice join-to-create (admin)' },
            { cmd: '!mungabee-supportticket', desc: 'Post support ticket menu' },
            { cmd: '!sendit', desc: 'Forward a message (admin)' }
        ];

        res.json({
            status: botReady ? 'online' : 'offline',
            presence,
            uptime,
            guildCount,
            commands,
            timestamp: new Date().toISOString()
        });
    } catch (e) {
        res.status(500).json({ error: 'internal' });
    }
});

const server = app.listen(PORT, () => {
    console.log(`✅ HTTP Server running on port ${PORT}`);
    console.log(`🌍 Accessible at http://localhost:${PORT}`);
});


const { handleCommand, restoreBumpReminders, helpdeskOrigins, commandHandlers } = require('./commands');
const { handleVoiceStateUpdate, checkAfkUsers, autoCleanupVoiceLogs } = require('./voiceHandler');

client.once('ready', () => {
    console.log(`${BOT_INFO.name} v${BOT_INFO.version} is online!`);
    console.log(`Logged in as ${client.user.tag}`);
    
    restoreBumpReminders(client);
    console.log('✅ Bump reminders restored from file');

    // Ping Pingbot direkt beim Start
    sendPingToPingBot();
    console.log('✅ Ping an Pingbot beim Start gesendet');

    // Start AFK checker (every minute)
    setInterval(() => checkAfkUsers(client), 60 * 1000);
    console.log('✅ Voice AFK checker started');

    // Start voice log cleanup (every 5 hours)
    setInterval(() => autoCleanupVoiceLogs(client), 5 * 60 * 60 * 1000);
    console.log('✅ Voice log auto-cleanup started (every 5 hours)');

    updateGameStatus();
    setInterval(updateGameStatus, 3600000);
    try {
        // restore previously scheduled cleanup intervals so they survive restarts
        restoreCleanupIntervals(client);
    } catch (e) { }

    // periodic backup of common JSON state files to ensure persistence
    const knownFiles = [
        'cleanup_intervals.json',
        'birthdays.json',
        'github_links.json',
        'bump_reminders.json',
        'bump_reminders_ping.json',
        'voice_settings.json',
        'twitch_settings.json'
    ];
    persistence.schedulePeriodicBackup(knownFiles, 5 * 60 * 1000);

    // also flush on exit signals
    const flush = () => { try { persistence.backupFiles(knownFiles); } catch (e) { } };
    process.on('exit', flush);
    process.on('SIGINT', () => { flush(); process.exit(); });
    process.on('SIGTERM', () => { flush(); process.exit(); });
});

function updateGameStatus() {
    gameTimer++;
    if (gameTimer > MAX_HOURS) {
        gameTimer = 2;
    }
    
    client.user.setPresence({
        activities: [{
            name: 'Dead by Daylight 💀',
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
    if (message.author.id === '302050872383242240') {
        if (message.embeds.length > 0) {
            const embed = message.embeds[0];
            if (embed.description) {
                const desc = embed.description.toLowerCase();
                // Englische, deutsche und weitere Varianten
                const bumpKeywords = [
                    'bump done',
                    ':thumbsup:',
                    'bumped',
                    'bump erfolgreich',
                    'erfolgreich gebumpt',
                    'server wurde gebumpt',
                    'du kannst den server in 2 stunden wieder bumpen',
                    'bump ist durch',
                    'bump abgeschlossen',
                    'bump wurde durchgeführt',
                    'bump effectué', // Französisch
                    'bump completado', // Spanisch
                    'bump effettuato', // Italienisch
                    'bump realizado', // Portugiesisch
                ];
                if (bumpKeywords.some(k => desc.includes(k))) {
                    console.log(`Bump detected in channel: ${message.channel.name}`);
                    const { setBumpReminder } = require('./commands');
                    setBumpReminder(message.channel, message.guild);
                }
            }
        }
        return;
    }

    if (message.author.bot) return;

    const messageId = message.id;
    if (processedMessages.has(messageId)) return;
    processedMessages.add(messageId);

    setTimeout(() => {
        processedMessages.delete(messageId);
    }, 60000);

    handleCommand(message, BOT_INFO);
});

client.on('voiceStateUpdate', (oldState, newState) => {
    handleVoiceStateUpdate(oldState, newState);
});

// Handle helpdesk select menu interactions
client.on('interactionCreate', async (interaction) => {
    if (!interaction.isStringSelectMenu()) return;
    const customId = interaction.customId;
    if (!['helpdesk_select','support_select'].includes(customId)) return;

    const choice = interaction.values[0];
    // Basic replies for each category — adjust text as needed
    let replyText = '';
    switch (choice) {
        case 'help_all': replyText = 'Full command list: run `!help` in your server channel to see all commands.'; break;
        case 'help_voice': replyText = 'Voice help: run `!helpyvoice` in your server channel for voice commands.'; break;
        case 'help_secure': replyText = 'Security help: run `!helpysecure` to see moderation commands.'; break;
        case 'help_twitch': replyText = 'Twitch help: run `!helpytwitch` for Twitch integration commands.'; break;
        case 'help_github': replyText = 'GitHub help: run `!helpygithub` to manage GitHub linking.'; break;
        case 'help_bump': replyText = 'Bump help: run `!helpybump` for bump/reminder commands.'; break;
        case 'help_birth': replyText = 'Birthday help: run `!helpybirth` to configure birthdays.'; break;
        case 'support_help': replyText = 'Support / Tickets: run `!mungabee-supportticket` in a server channel to post the support ticket menu. Follow the prompts to post the ticket embed to a target channel.'; break;
        default: replyText = 'No information available for this selection.';
    }

    // Ephemeral reply so only the user sees it
    try {
        await interaction.reply({ content: replyText, ephemeral: true });
    } catch (err) {
        // fallback: try to DM the user
        try { await interaction.user.send(replyText); } catch (e) { /* ignore */ }
    }

    // Optionally, send a DM with the same info (users sometimes prefer DMs)
    try {
        await interaction.user.send(`You requested help for: ${choice}\n\n${replyText}`);
    } catch (e) {
        // can't DM user — ignore
    }

    // If support_select, create a private ticket channel
    if (customId === 'support_select') {
        try {
            const { loadTicketsConfig } = require('./commands');
            const cfg = loadTicketsConfig();
            const guild = interaction.guild;
            const user = interaction.user;
            const selectionMap = {
                support_technical: 'Technical Issue',
                support_spam: 'Spam / Scam',
                support_abuse: 'Abuse / Harassment',
                support_ad: 'Advertising / Recruitment',
                support_bug: 'Bug / Feature Request',
                support_other: 'Other'
            };
            const selectionLabel = selectionMap[choice] || choice;

            const chanName = `ticket-${user.username.toLowerCase().replace(/[^a-z0-9]/g,'')}-${Date.now()%10000}`;
            const overwrites = [];
            // deny everyone
            overwrites.push({ id: guild.id, deny: ['ViewChannel'] });
            // allow user
            overwrites.push({ id: user.id, allow: ['ViewChannel', 'SendMessages', 'ReadMessageHistory'] });
            // allow admins
            guild.roles.cache.filter(r => r.permissions.has(PermissionsBitField.Flags.Administrator)).forEach(role => {
                overwrites.push({ id: role.id, allow: ['ViewChannel', 'SendMessages', 'ReadMessageHistory'] });
            });
            // allow bot
            overwrites.push({ id: client.user.id, allow: ['ViewChannel', 'SendMessages', 'ReadMessageHistory', 'ManageChannels'] });

            const ticketChannel = await guild.channels.create({ name: chanName, type: 0, permissionOverwrites: overwrites });

            // send initial embed + buttons
            const { EmbedBuilder, ActionRowBuilder, ButtonBuilder, ButtonStyle } = require('discord.js');
            const embed = new EmbedBuilder()
                .setColor('#2f3136')
                .setTitle(`Ticket: ${selectionLabel}`)
                .setDescription(`Opened by <@${user.id}> (${user.id})\n\nPls continue and write your issue. We will be here for you as quick as we can!`)
                .setFooter({ text: `Category: ${selectionLabel}` });

            const row = new ActionRowBuilder().addComponents(
                new ButtonBuilder().setCustomId('ticket_close').setLabel('Close Ticket').setStyle(ButtonStyle.Danger),
                new ButtonBuilder().setCustomId('ticket_log').setLabel('Log Ticket').setStyle(ButtonStyle.Secondary),
                new ButtonBuilder().setCustomId('ticket_save').setLabel('Save Transcript').setStyle(ButtonStyle.Primary)
            );

            await ticketChannel.send({ content: `<@${user.id}>`, embeds: [embed], components: [row] });
            await interaction.reply({ content: `✅ Ticket created: ${ticketChannel}`, ephemeral: true });

            // store ticket meta in memory for button handlers
            if (!global.ticketMeta) global.ticketMeta = new Map();
            global.ticketMeta.set(ticketChannel.id, { userId: user.id, category: selectionLabel, originMessageId: interaction.message.id, guildId: guild.id });

        } catch (err) {
            console.error('Support select handler error:', err);
            try { await interaction.reply({ content: '❌ Failed to create ticket channel.', ephemeral: true }); } catch (e) { }
        }
    }

    // Note: Discord does not allow sending ephemeral replies to a different channel than the interaction.
    // We stored the origin channel when the helpdesk was created; if you want a public follow-up in the
    // origin channel, you can uncomment the block below (will be visible to everyone in that channel).
    /*
    try {
        const originChannelId = helpdeskOrigins.get(interaction.message.id);
        if (originChannelId) {
            const originChannel = client.channels.cache.get(originChannelId);
            if (originChannel && originChannel.permissionsFor(client.user).has(PermissionsBitField.Flags.SendMessages)) {
                originChannel.send(`<@${interaction.user.id}> selected a help category: **${choice}**`);
            }
        }
    } catch (e) { }
    */
});

// Handle ticket buttons (close / log / save)
client.on('interactionCreate', async (interaction) => {
    if (!interaction.isButton()) return;
    const id = interaction.customId;
    const channel = interaction.channel;
    const guild = interaction.guild;
    try {
        // ensure this is a ticket channel
        const meta = global.ticketMeta ? global.ticketMeta.get(channel.id) : null;
        if (!meta) {
            await interaction.reply({ content: 'This button is only available inside a ticket channel.', ephemeral: true });
            return;
        }

        const { loadTicketsConfig } = require('./commands');
        const cfg = loadTicketsConfig();
        const logChannelId = cfg[guild.id]?.logChannelId;

        if (id === 'ticket_close') {
            await interaction.reply({ content: 'Closing ticket...', ephemeral: true });
            // delete channel after short delay to allow reply
            setTimeout(() => { channel.delete().catch(() => {}); if (global.ticketMeta) global.ticketMeta.delete(channel.id); }, 1000);
            return;
        }

        // fetch messages and build transcript
        let messages = [];
        let lastId;
        while (true) {
            const options = { limit: 100 };
            if (lastId) options.before = lastId;
            const batch = await channel.messages.fetch(options);
            if (!batch) break;
            messages = messages.concat(Array.from(batch.values()).map(m => ({ author: m.author.tag, id: m.author.id, content: m.content, time: m.createdAt.toISOString() })));
            if (batch.size < 100) break;
            lastId = batch.last().id;
        }
        // reverse to chronological
        messages = messages.reverse();
        const transcript = messages.map(m => `[${m.time}] ${m.author} (${m.id}): ${m.content}`).join('\n');

        if (id === 'ticket_save' || id === 'ticket_log') {
            if (!logChannelId) {
                await interaction.reply({ content: 'No log channel configured for this server. Ask an admin to run !m-ticketsystem.', ephemeral: true });
                return;
            }
            const filename = `ticket_${guild.id}_${channel.id}_${Date.now()}.txt`;
            const fs = require('fs');
            fs.writeFileSync(filename, transcript);
            const logChan = guild.channels.cache.get(logChannelId);
            if (logChan) {
                await logChan.send({ content: `Ticket transcript from ${channel.name} (created by <@${meta.userId}>):`, files: [filename] });
            }
            // tidy up local file
            try { fs.unlinkSync(filename); } catch (e) { }
            await interaction.reply({ content: '✅ Ticket transcript saved to log channel.', ephemeral: true });
            return;
        }
    } catch (err) {
        console.error('Ticket button handler error:', err);
        try { await interaction.reply({ content: 'An error occurred while handling the ticket action.', ephemeral: true }); } catch (e) { }
    }
});

// Map slash (chat input) commands to existing prefix handlers where possible
client.on('interactionCreate', async (interaction) => {
    if (!interaction.isChatInputCommand()) return;

    const name = interaction.commandName; // e.g. 'help' -> maps to '!help'
    const handler = commandHandlers['!' + name];
    if (!handler) {
        await interaction.reply({ content: 'This slash command has no mapped prefix handler.', ephemeral: true });
        return;
    }

    let replied = false;
    const doReply = async (payload) => {
        const p = (typeof payload === 'string') ? { content: payload } : payload || {};
        if (!replied) {
            replied = true;
            return interaction.reply(Object.assign({}, p, { ephemeral: true }));
        }
        return interaction.followUp(Object.assign({}, p, { ephemeral: true }));
    };

    const fakeMessage = {
        content: '!' + name,
        author: interaction.user,
        member: interaction.member,
        guild: interaction.guild,
        channel: {
            send: (p) => doReply(p)
        },
        reply: (p) => doReply(p)
    };

    try {
        await handler(fakeMessage);
    } catch (err) {
        console.error('Slash->prefix handler error:', err);
        try { await interaction.reply({ content: 'Command failed (see logs).', ephemeral: true }); } catch (e) { /* ignore */ }
    }
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