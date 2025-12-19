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