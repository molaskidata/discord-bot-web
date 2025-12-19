// Original archived copy of Bothbots/Pingbot/pingbot.js
// (Saved by archive operation)
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const path = require('path');
const fs = require('fs');
require('dotenv').config({ path: path.join(__dirname, '.env') });
const { Client, GatewayIntentBits, EmbedBuilder } = require('discord.js');
// Add GuildMembers intent so we can inspect other bot members in the guild
// Note: Presence information requires the GuildPresences intent to be enabled in the bot settings
const client = new Client({ intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMessages, GatewayIntentBits.MessageContent, GatewayIntentBits.GuildMembers, GatewayIntentBits.GuildPresences] });

const PING_GUILD_ID = '1415044198792691858';
const PING_CHANNEL_ID = '1448640396359106672';

// --- Monitor configuration for status embeds (user-requested) ---
const MONITOR_GUILD_ID = '1410329844272595050';
const MONITOR_CHANNEL_ID = '1450161151869452360';

const MONITOR_STATE_FILE = 'ping_monitor_state.json';
function loadMonitorState() {
    try { return fs.existsSync(MONITOR_STATE_FILE) ? JSON.parse(fs.readFileSync(MONITOR_STATE_FILE)) : { messages: {}, lastSeen: {} }; } catch (e) { return { messages: {}, lastSeen: {} }; }
}
function saveMonitorState(st) { try { fs.writeFileSync(MONITOR_STATE_FILE, JSON.stringify(st, null, 2)); } catch (e) { } }

const MONITOR_TARGETS = [
    { key: 'mainbot', display: '!Code.Master() Stats', color: 0x008B8B, hints: ['Mainbot', 'Main', 'Code.Master', 'Mainbnbot'], pingCmd: '!pingmeee', expectReplyContains: '!pongez' },
    { key: 'pirate', display: 'Mary the red Stats', color: 0x8B0000, hints: ['Pirat', 'Pirate', 'Mary'], pingCmd: '!ping', expectReplyContains: 'Pong' }
];

let monitorState = loadMonitorState();

const DISBOARD_BOT_ID = '302050872383242240';
let bumpReminders = new Map();

const BUMP_FILE = 'bump_reminders_ping.json';
function loadBumpReminders() {
    if (fs.existsSync(BUMP_FILE)) {
        return JSON.parse(fs.readFileSync(BUMP_FILE));
    }
    return {};
}
function saveBumpRemindersToFile(data) {
    fs.writeFileSync(BUMP_FILE, JSON.stringify(data, null, 2));
}

function restoreBumpReminders() {
    const storedReminders = loadBumpReminders();
    const now = Date.now();

    Object.entries(storedReminders).forEach(([channelId, data]) => {
        const timeLeft = data.triggerTime - now;

        if (timeLeft <= 0) {
            delete storedReminders[channelId];
            saveBumpRemindersToFile(storedReminders);
            return;
        }

        const channel = client.channels.cache.get(channelId);
        if (!channel) {
            delete storedReminders[channelId];
            saveBumpRemindersToFile(storedReminders);
            return;
        }

        const reminderTimeout = setTimeout(() => {
            channel.send('⏰ **Bump Reminder!** ⏰\n\nThe server can be bumped again now! Use `/bump` to bump the server on Disboard! 🚀');
            bumpReminders.delete(channelId);

            const currentReminders = loadBumpReminders();
            delete currentReminders[channelId];
            saveBumpRemindersToFile(currentReminders);
        }, timeLeft);

        bumpReminders.set(channelId, reminderTimeout);
    });
}

client.on('ready', () => {
    console.log('PingBot is online!');
    client.user.setPresence({
        activities: [{
            name: '"Das große Buch der Herzschlag-Bots"',
            type: 1,
            details: 'Seite 102 von 376',
            state: 'Vorlesen: auf Seite 171 von 304'
        }],
        status: 'online'
    });

    restoreBumpReminders();
    console.log('✅ Bump reminders restored from file');

    function sendPingToMainBot() {
        const guild = client.guilds.cache.get(PING_GUILD_ID);
        if (guild) {
            const channel = guild.channels.cache.get(PING_CHANNEL_ID);
            if (channel) {
                channel.send('!pingmeee');
            }
        }
    }
    sendPingToMainBot();
    setInterval(sendPingToMainBot, 90 * 60 * 1000);
    // initialize monitor messages and start periodic status updates (every 60s)
    try {
        (async () => {
            try { await ensureMonitorMessages(); } catch (e) { console.error('ensureMonitorMessages failed', e); }
            try { await updateAllMonitors(); } catch (e) { console.error('updateAllMonitors initial failed', e); }
            setInterval(() => { updateAllMonitors().catch(e => console.error('updateAllMonitors error', e)); }, 60 * 1000);
        })();
    } catch (e) { console.error('Monitor init error', e); }
});

// ---- Monitor helpers ----
async function findBotMember(guild, hints) {
    try {
        await guild.members.fetch(); // populate cache (needs GuildMembers intent)
        for (const [, member] of guild.members.cache) {
            if (!member.user.bot) continue;
            const uname = (member.user.username || '').toLowerCase();
            const dname = (member.displayName || '').toLowerCase();
            for (const h of hints) {
                const lh = (h || '').toLowerCase();
                if (uname.includes(lh) || dname.includes(lh)) return member;
            }
        }
    } catch (e) { }
    return null;
}

async function ensureMonitorMessages() {
    let guild;
    try { guild = await client.guilds.fetch(MONITOR_GUILD_ID); } catch (e) { console.error('ensureMonitorMessages: fetch guild failed', e); throw e; }
    let channel;
    try { channel = await guild.channels.fetch(MONITOR_CHANNEL_ID); } catch (e) { console.error('ensureMonitorMessages: fetch channel failed', e); throw e; }

    for (const target of MONITOR_TARGETS) {
        try {
            const mid = monitorState.messages[target.key];
            if (mid) {
                const msg = await channel.messages.fetch(mid).catch(() => null);
                if (msg) continue; // exists and editable
            }
            const perms = channel.permissionsFor ? channel.permissionsFor(client.user) : null;
            if (perms && (!perms.has('ViewChannel') || !perms.has('SendMessages') || !perms.has('EmbedLinks'))) {
                console.warn(`ensureMonitorMessages: missing permissions for ${target.key} in channel ${MONITOR_CHANNEL_ID}`);
                continue;
            }
            const emb = new EmbedBuilder()
                .setTitle(target.display)
                .setDescription('Initializing status monitor...')
                .setColor(target.color)
                .setTimestamp();
            const sent = await channel.send({ embeds: [emb] });
            if (sent && sent.id) { monitorState.messages[target.key] = sent.id; saveMonitorState(monitorState); }
        } catch (e) { console.error('ensureMonitorMessages per-target error', target.key, e); }
    }
}

function emojiForStatus(s) {
    if (s === 'ONLINE') return '🟢';
    if (s === 'STANDBY') return '🟠';
    if (s === 'CRASHED') return '🔴';
    return '⚫';
}

function formatIso(d) { try { return new Date(d).toLocaleString('de-DE', { hour12: false, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' }) } catch (e) { return d || '—' } }

function waitForReply(channel, authorId, timeoutMs = 5000) {
    return new Promise((resolve) => {
        const col = channel.createMessageCollector({ filter: m => m.author.id === authorId, time: timeoutMs, max: 1 });
        col.on('collect', (m) => resolve(m));
        col.on('end', (collected) => { if (collected.size === 0) resolve(null); });
    });
}

async function checkTargetStatus(guild, channel, target) {
    const member = await findBotMember(guild, target.hints);
    let statusLabel = 'OFFLINE';
    let lastSeen = monitorState.lastSeen[target.key] || null;
    if (!member) {
        return { status: 'OFFLINE', lastSeen };
    }
    const pres = member.presence ? (member.presence.status || 'offline') : 'offline';
    const now = Date.now();
    if (pres !== 'offline') {
        // bot appears online/idle/dnd — treat as live
        statusLabel = pres === 'online' ? 'ONLINE' : 'STANDBY';
        lastSeen = new Date(now).toISOString();
        monitorState.lastSeen[target.key] = lastSeen;
        saveMonitorState(monitorState);
        return { status: statusLabel, lastSeen };
    }
    // presence is offline
    if (lastSeen && (now - Date.parse(lastSeen) < (5 * 60 * 1000))) {
        statusLabel = 'CRASHED';
    } else {
        statusLabel = 'OFFLINE';
    }
    return { status: statusLabel, lastSeen };
}

async function updateAllMonitors() {
    let guild;
    try { guild = await client.guilds.fetch(MONITOR_GUILD_ID); } catch (e) { console.error('updateAllMonitors: fetch guild failed', e); return; }
    let channel;
    try { channel = await guild.channels.fetch(MONITOR_CHANNEL_ID); } catch (e) { console.error('updateAllMonitors: fetch channel failed', e); return; }

    for (const target of MONITOR_TARGETS) {
        try {
            const res = await checkTargetStatus(guild, channel, target);
            const mid = monitorState.messages[target.key];
            const embed = new EmbedBuilder()
                .setTitle(target.display)
                .setColor(target.color)
                .addFields(
                    { name: 'Last Update', value: res.lastSeen ? formatIso(res.lastSeen) : '—', inline: true },
                    { name: 'Status', value: `${emojiForStatus(res.status)} ${res.status}`, inline: true }
                )
                .setTimestamp();
            if (mid) {
                const msg = await channel.messages.fetch(mid).catch(() => null);
                if (msg && msg.editable) {
                    await msg.edit({ embeds: [embed] }).catch(e => console.error('updateAllMonitors: edit failed', e));
                    continue;
                }
            }
            const sent = await channel.send({ embeds: [embed] }).catch(e => { console.error('updateAllMonitors: send failed', e); return null; });
            if (sent) { monitorState.messages[target.key] = sent.id; saveMonitorState(monitorState); }
        } catch (e) { console.error('updateAllMonitors per-target error', target.key, e); }
    }
}

client.on('messageCreate', (message) => {
    if (message.content === '!pingme') {
        message.channel.send('!ponggg');
        return;
    }

    if (message.content === '!delbumpreminder2') {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        if (bumpReminders.has(message.channel.id)) {
            clearTimeout(bumpReminders.get(message.channel.id));
            bumpReminders.delete(message.channel.id);
            const storedReminders = loadBumpReminders();
            delete storedReminders[message.channel.id];
            saveBumpRemindersToFile(storedReminders);
            message.channel.send('🗑️ Bump reminder for this channel has been deleted.');
        } else {
            message.channel.send('❌ No active bump reminder for this channel.');
        }
        return;
    }

    if (message.content === '!setbumpreminder2') {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        setBumpReminder(message.channel, message.guild);
        return;
    }

    if (message.content === '!bumpstatus') {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        if (bumpReminders.has(message.channel.id)) {
            message.channel.send('⏳ Bump reminder is active for this channel. You\'ll be notified when the next bump is available.');
        } else {
            message.channel.send('❌ No active bump reminder for this channel. Use `!setbumpreminder` to set one manually.');
        }
        return;
    }

    if (message.content === '!bumphelp') {
        message.channel.send(
            '**🤖 Bump Reminder System Help**\n\n' +
            '**Automatic Detection:** I automatically detect when you use `/bump` and set a 2-hour reminder!\n\n' +
            '**Manual Commands:**\n' +
            '`!setbumpreminder` - Manually set a 2-hour bump reminder *(admin only)*\n' +
            '`!bumpstatus` - Check if there\'s an active reminder for this channel *(admin only)*\n' +
            '`!bumphelp` - Show this help message\n\n' +
            '**How it works:** After a successful bump, I\'ll remind you exactly when the next bump is available (2 hours later)! 🚀'
        );
        return;
    }

    // General PingBot help (avoid conflict with Mainbot !help)
    if (message.content === '!phelp') {
        const { EmbedBuilder } = require('discord.js');
        const embed = new EmbedBuilder()
            .setColor('#5865F2')
            .setTitle('PingBot — Help')
            .setDescription('Self-contained help for PingBot (ping & bump reminders).')
            .addFields(
                { name: 'Ping', value: '`!pingme` - Basic ping/pong check', inline: false },
                { name: 'Bump Reminder Commands', value: '`!setbumpreminder2` - Set a 2-hour bump reminder (admin only)\n`!delbumpreminder2` - Delete the active bump reminder (admin only)\n`!bumpstatus` - Show status of bump reminder (admin only)\n`!bumphelp` - Show detailed bump help', inline: false }
            )
            .setFooter({ text: 'PingBot — bump helper' });

        message.channel.send({ embeds: [embed] });
        return;
    }

    if (message.author.id === DISBOARD_BOT_ID) {
        if (message.embeds.length > 0) {
            const embed = message.embeds[0];
            if (embed.description &&
                (embed.description.includes('Bump done') ||
                    embed.description.includes(':thumbsup:') ||
                    embed.description.toLowerCase().includes('bumped'))) {

                console.log(`Bump detected in channel: ${message.channel.name}`);
                setBumpReminder(message.channel, message.guild);
            }
        }
    }
});

function setBumpReminder(channel, guild) {
    const channelId = channel.id;
    const guildId = guild.id;

    if (bumpReminders.has(channelId)) {
        clearTimeout(bumpReminders.get(channelId));
    }

    const triggerTime = Date.now() + (2 * 60 * 60 * 1000);

    const reminderTimeout = setTimeout(() => {
        channel.send('⏰ **Bump Reminder!** ⏰\n\nThe server can be bumped again now! Use `/bump` to bump the server on Disboard! 🚀');
        bumpReminders.delete(channelId);

        const storedReminders = loadBumpReminders();
        delete storedReminders[channelId];
        saveBumpRemindersToFile(storedReminders);
    }, 2 * 60 * 60 * 1000);

    bumpReminders.set(channelId, reminderTimeout);

    const storedReminders = loadBumpReminders();
    storedReminders[channelId] = {
        guildId: guildId,
        triggerTime: triggerTime
    };
    saveBumpRemindersToFile(storedReminders);

    channel.send('✅ Bump reminder set! I\'ll remind you in 2 hours when the next bump is available.');
}

client.login(process.env.PINGBOT_TOKEN);

// ...entfernt, wird unten korrekt eingefügt...

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const path = require('path');
const fs = require('fs');
require('dotenv').config({ path: path.join(__dirname, '.env') });
const { Client, GatewayIntentBits, EmbedBuilder } = require('discord.js');
// Add GuildMembers intent so we can inspect other bot members in the guild
// Note: Presence information requires the GuildPresences intent to be enabled in the bot settings
const client = new Client({ intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMessages, GatewayIntentBits.MessageContent, GatewayIntentBits.GuildMembers, GatewayIntentBits.GuildPresences] });

const PING_GUILD_ID = '1415044198792691858';
const PING_CHANNEL_ID = '1448640396359106672';

// --- Monitor configuration for status embeds (user-requested) ---
const MONITOR_GUILD_ID = '1410329844272595050';
const MONITOR_CHANNEL_ID = '1450161151869452360';

const MONITOR_STATE_FILE = 'ping_monitor_state.json';
function loadMonitorState() {
    try { return fs.existsSync(MONITOR_STATE_FILE) ? JSON.parse(fs.readFileSync(MONITOR_STATE_FILE)) : { messages: {}, lastSeen: {} }; } catch (e) { return { messages: {}, lastSeen: {} }; }
}
function saveMonitorState(st) { try { fs.writeFileSync(MONITOR_STATE_FILE, JSON.stringify(st, null, 2)); } catch (e) { } }

const MONITOR_TARGETS = [
    { key: 'mainbot', display: '!Code.Master() Stats', color: 0x008B8B, hints: ['Mainbot', 'Main', 'Code.Master', 'Mainbnbot'], pingCmd: '!pingmeee', expectReplyContains: '!pongez' },
    { key: 'pirate', display: 'Mary the red Stats', color: 0x8B0000, hints: ['Pirat', 'Pirate', 'Mary'], pingCmd: '!ping', expectReplyContains: 'Pong' }
];

let monitorState = loadMonitorState();

const DISBOARD_BOT_ID = '302050872383242240';
let bumpReminders = new Map();

const BUMP_FILE = 'bump_reminders_ping.json';
function loadBumpReminders() {
    if (fs.existsSync(BUMP_FILE)) {
        return JSON.parse(fs.readFileSync(BUMP_FILE));
    }
    return {};
}
function saveBumpRemindersToFile(data) {
    fs.writeFileSync(BUMP_FILE, JSON.stringify(data, null, 2));
}

function restoreBumpReminders() {
    const storedReminders = loadBumpReminders();
    const now = Date.now();

    Object.entries(storedReminders).forEach(([channelId, data]) => {
        const timeLeft = data.triggerTime - now;

        if (timeLeft <= 0) {
            delete storedReminders[channelId];
            saveBumpRemindersToFile(storedReminders);
            return;
        }

        const channel = client.channels.cache.get(channelId);
        if (!channel) {
            delete storedReminders[channelId];
            saveBumpRemindersToFile(storedReminders);
            return;
        }

        const reminderTimeout = setTimeout(() => {
            channel.send('⏰ **Bump Reminder!** ⏰\n\nThe server can be bumped again now! Use `/bump` to bump the server on Disboard! 🚀');
            bumpReminders.delete(channelId);

            const currentReminders = loadBumpReminders();
            delete currentReminders[channelId];
            saveBumpRemindersToFile(currentReminders);
        }, timeLeft);

        bumpReminders.set(channelId, reminderTimeout);
    });
}

client.on('ready', () => {
    console.log('PingBot is online!');
    client.user.setPresence({
        activities: [{
            name: '"Das große Buch der Herzschlag-Bots"',
            type: 1,
            details: 'Seite 102 von 376',
            state: 'Vorlesen: auf Seite 171 von 304'
        }],
        status: 'online'
    });

    restoreBumpReminders();
    console.log('✅ Bump reminders restored from file');

    function sendPingToMainBot() {
        const guild = client.guilds.cache.get(PING_GUILD_ID);
        if (guild) {
            const channel = guild.channels.cache.get(PING_CHANNEL_ID);
            if (channel) {
                channel.send('!pingmeee');
            }
        }
    }
    sendPingToMainBot();
    setInterval(sendPingToMainBot, 90 * 60 * 1000);
    // initialize monitor messages and start periodic status updates (every 60s)
    try {
        (async () => {
            try { await ensureMonitorMessages(); } catch (e) { console.error('ensureMonitorMessages failed', e); }
            try { await updateAllMonitors(); } catch (e) { console.error('updateAllMonitors initial failed', e); }
            setInterval(() => { updateAllMonitors().catch(e => console.error('updateAllMonitors error', e)); }, 60 * 1000);
        })();
    } catch (e) { console.error('Monitor init error', e); }
});