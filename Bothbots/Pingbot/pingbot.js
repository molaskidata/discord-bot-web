    // ...entfernt, wird unten korrekt eingefügt...

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const path = require('path');
const fs = require('fs');
require('dotenv').config({ path: path.join(__dirname, '.env') });
const { Client, GatewayIntentBits } = require('discord.js');
const client = new Client({ intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMessages, GatewayIntentBits.MessageContent] });

const PING_GUILD_ID = '1415044198792691858';
const PING_CHANNEL_ID = '1448640396359106672';

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
});

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