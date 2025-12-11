const path = require('path');
require('dotenv').config({ path: path.join(__dirname, '.env') });
const { Client, GatewayIntentBits, ActivityType } = require('discord.js');

const { handleCommand } = require('./commands');

const BOT_INFO = {
    name: "PirateBot",
    version: "1.0.0",
    author: "mungabee/ozzygirl"
};

const client = new Client({
    intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMessages,
        GatewayIntentBits.MessageContent
    ]
});

client.once('ready', () => {
    console.log(`${BOT_INFO.name} v${BOT_INFO.version} is online!`);
    console.log(`Logged in as ${client.user.tag}`);
    
    client.user.setPresence({
        activities: [{
            name: 'Sea of Thieves ⚓',
            type: ActivityType.Playing,
            details: 'Sailing the seven seas',
            state: 'Ahoy, mateys!'
        }],
        status: 'online'
    });
    
    console.log('🏴‍☠️ PirateBot ready to sail!');
});

client.on('messageCreate', (message) => {
    if (message.author.bot) return;
    
    if (message.content.startsWith('!')) {
        handleCommand(message, BOT_INFO);
    }
    
    const lowerContent = message.content.toLowerCase();
    if (lowerContent.includes('ahoy') && !message.content.startsWith('!')) {
        message.reply('Ahoy there, matey! 🏴‍☠️');
    }
});

client.login(process.env.DISCORD_TOKEN);

process.on('unhandledRejection', error => {
    console.error('Unhandled promise rejection:', error);
});

console.log(`Starting ${BOT_INFO.name} v${BOT_INFO.version}...`);
console.log('Pirate bot initializing... 🏴‍☠️');
