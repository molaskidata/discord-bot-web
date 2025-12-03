
require('dotenv').config();
const { Client, GatewayIntentBits } = require('discord.js');
const client = new Client({ intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMessages, GatewayIntentBits.MessageContent] });

// Channel and guild IDs for pinging
const PING_GUILD_ID = '1415044198792691858'; // Neuer Server ID
const PING_CHANNEL_ID = '1440998057016557619'; // Neuer Channel ID

// Bump reminder system
const DISBOARD_BOT_ID = '302050872383242240'; // Official Disboard bot ID
let bumpReminders = new Map(); // Store active reminders by channel ID

client.on('ready', () => {
    console.log('PingBot is online!');
    client.user.setPresence({
        activities: [{
            name: '"Das große Buch der Herzschlag-Bots"',
            type: 1, // Streaming/Streamt
            details: 'Seite 102 von 376',
            state: 'Vorlesen: auf Seite 171 von 304'
        }],
        status: 'online'
    });

    // Pingbot sends !pingmeee every 1.5 hours
    function sendPingToMainBot() {
        const guild = client.guilds.cache.get(PING_GUILD_ID);
        if (guild) {
            const channel = guild.channels.cache.get(PING_CHANNEL_ID);
            if (channel) {
                channel.send('!pingmeee');
            }
        }
    }
    // Sofort beim Start einmal senden (optional)
    sendPingToMainBot();
    // Dann alle 1,5 Stunden
    setInterval(sendPingToMainBot, 90 * 60 * 1000);
});

client.on('messageCreate', (message) => {
    // Handle ping command
    if (message.content === '!pingme') {
        message.channel.send('!ponggg');
        return;
    }

    // Manual bump reminder command
    if (message.content === '!setbumpreminder') {
        setBumpReminder(message.channel, message.guild);
        return;
    }

    // Check bump reminder status
    if (message.content === '!bumpstatus') {
        if (bumpReminders.has(message.channel.id)) {
            message.channel.send('⏳ Bump reminder is active for this channel. You\'ll be notified when the next bump is available.');
        } else {
            message.channel.send('❌ No active bump reminder for this channel. Use `!setbumpreminder` to set one manually.');
        }
        return;
    }

    // Help command for bump system
    if (message.content === '!bumphelp') {
        message.channel.send(
            '**🤖 Bump Reminder System Help**\n\n' +
            '**Automatic Detection:** I automatically detect when you use `/bump` and set a 2-hour reminder!\n\n' +
            '**Manual Commands:**\n' +
            '`!setbumpreminder` - Manually set a 2-hour bump reminder\n' +
            '`!bumpstatus` - Check if there\'s an active reminder for this channel\n' +
            '`!bumphelp` - Show this help message\n\n' +
            '**How it works:** After a successful bump, I\'ll remind you exactly when the next bump is available (2 hours later)! 🚀'
        );
        return;
    }

    // Monitor for Disboard bump confirmations
    if (message.author.id === DISBOARD_BOT_ID) {
        // Check if it's a successful bump message (Disboard sends embeds)
        if (message.embeds.length > 0) {
            const embed = message.embeds[0];
            // Look for bump confirmation messages (they usually contain "Bump done" or similar)
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

// Function to set a 2-hour bump reminder
function setBumpReminder(channel, guild) {
    const channelId = channel.id;
    
    // Clear any existing reminder for this channel
    if (bumpReminders.has(channelId)) {
        clearTimeout(bumpReminders.get(channelId));
    }
    
    // Set new 2-hour reminder (2 hours = 2 * 60 * 60 * 1000 ms)
    const reminderTimeout = setTimeout(() => {
        channel.send('⏰ **Bump Reminder!** ⏰\n\nThe server can be bumped again now! Use `/bump` to bump the server on Disboard! 🚀');
        bumpReminders.delete(channelId);
    }, 2 * 60 * 60 * 1000); // 2 hours
    
    // Store the timeout reference
    bumpReminders.set(channelId, reminderTimeout);
    
    // Send confirmation message
    channel.send('✅ Bump reminder set! I\'ll remind you in 2 hours when the next bump is available.');
}

client.login(process.env.PINGBOT_TOKEN);
//yess i got it, look i am a genius!