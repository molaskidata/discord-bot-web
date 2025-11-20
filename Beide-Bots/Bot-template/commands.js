const fs = require('fs');
// commands.js
// All Discord bot command logic is moved here for better structure and overview.

const { getRandomResponse } = require('./utils');

// Response arrays (can be moved to a separate file if needed)
const programmingMemes = [
    "It works on my machine! 🤷‍♂️",
    "Copy from Stack Overflow? It's called research! 📚",
    "Why do programmers prefer dark mode? Because light attracts bugs! 💡🐛",
    "There are only 10 types of people: those who understand binary and those who don't! 🔢",
    "99 little bugs in the code... take one down, patch it around... 127 little bugs in the code! 🐛",
    "Debugging: Being the detective in a crime movie where you are also the murderer! 🔍",
    "Programming is like writing a book... except if you miss a single comma the whole thing is trash! 📚",
    "A SQL query goes into a bar, walks up to two tables and asks: 'Can I join you?' 🍺",
    "Why do Java developers wear glasses? Because they can't C# 👓",
    "How many programmers does it take to change a light bulb? None, that's a hardware problem! 💡",
    "My code doesn't always work, but when it does, I don't know why! 🤔",
    "Programming is 10% science, 20% ingenuity, and 70% getting the ingenuity to work with the science! ⚗️",
    "I don't always test my code, but when I do, I do it in production! 🚀",
    "Roses are red, violets are blue, unexpected '{' on line 32! 🌹",
    "Git commit -m 'fixed bug' // creates 5 new bugs 🔄"
];

const hiResponses = [
    "Heyho, how ya doing? ☕",
    "Hi! You coding right now? 💻", 
    "Hey, how is life going? 😊",
    "Hi creature, what's life on earth doing? 🌍"
];

const coffeeResponses = [
    "Time for coffee break! ☕ Who's joining?",
    "Coffee time! Let's fuel our coding session! ⚡",
    "Perfect timing! I was craving some coffee too ☕",
    "Coffee break = best break! Grab your mug! 🍵"
];

const motivationQuotes = [
    "Code like you're changing the world! 🌟",
    "Every bug is just a feature in disguise! 🐛✨",
    "You're not stuck, you're just debugging life! 🔧",
    "Keep coding, keep growing! 💪"
];

const goodnightResponses = [
    "Sweet dreams! Don't forget to push your code! 🌙",
    "Sleep tight! May your dreams be bug-free! 😴",
    "Good night! Tomorrow's code awaits! ⭐",
    "Rest well, coding warrior! 🛡️💤"
];

// Command handler map

const commandHandlers = {
    '!hi': (message) => message.reply(getRandomResponse(hiResponses)),
    '!github': (message) => message.reply("Check that out my friend! `https://github.com/molaskidata`"),
    '!coffee': (message) => message.reply(getRandomResponse(coffeeResponses)),
    '!meme': (message) => message.reply(getRandomResponse(programmingMemes)),
    '!motivation': (message) => message.reply(getRandomResponse(motivationQuotes)),
    '!gg': (message) => message.reply("GG WP! 🎉"),
    '!goodnight': (message) => message.reply(getRandomResponse(goodnightResponses)),
    '!help': (message) => message.reply('**Available Commands:**\n`!hi` - Say hello\n`!coffee` - Time for coffee!\n`!meme` - Programming memes\n `!github` - Bots Owner Github and my Repo! \n`!motivation` - Get motivated\n`!goodnight` - Good night messages\n`!ping` - Test bot\n`!info` - Bot info'),
    '!ping': (message) => message.reply('Pong! Bot is running 24/7'),
    '!pingmeee': (message) => {
        message.channel.send('!pongez');
    },
    '!info': (message, BOT_INFO) => message.reply(`Bot: ${BOT_INFO.name} v${BOT_INFO.version}\nStatus: Online 24/7`),
    '!commands': (message) => {
        message.reply(
            '**Available Commands:**\n' +
            '`!commands` - Show this list\n' +
            '`!congithubacc` - Connect your GitHub account\n' +
            '`!discongithubacc` - Disconnect your GitHub account\n' +
            '`!gitrank` - Show your GitHub commit level\n' +
            '`!gitleader` - Show the top 10 committers\n' +
            '`!hi`, `!coffee`, `!meme`, `!motivation`, `!goodnight`, `!ping`, `!info`, `!github`'
        );
    },
    '!congithubacc': (message) => {
        const discordId = message.author.id;
        const loginUrl = `http://localhost:3000/github/login?discordId=${discordId}`;
        message.reply(
            `To connect your GitHub account, click this link: ${loginUrl}\n` +
            'Authorize the app, then return to Discord!'
        );
    },
    // Disconnect GitHub account and remove role
    '!discongithubacc': async (message) => {
        const discordId = message.author.id;
        // Remove from JSON
        let data = {};
        if (fs.existsSync('github_links.json')) {
            data = JSON.parse(fs.readFileSync('github_links.json'));
        }
        if (data[discordId]) {
            delete data[discordId];
            fs.writeFileSync('github_links.json', JSON.stringify(data, null, 2));
            // Remove role
            try {
                const guild = message.guild;
                const member = await guild.members.fetch(discordId);
                await member.roles.remove('1440681068708630621');
                message.reply('Your GitHub account has been disconnected and the role removed.');
            } catch (err) {
                message.reply('Disconnected, but failed to remove the role.');
            }
        } else {
            message.reply('No GitHub account linked.');
        }
    }
};

function handleCommand(message, BOT_INFO) {
    const handler = commandHandlers[message.content];
    if (handler) {
        // Some commands need BOT_INFO
        if (message.content === '!info') {
            handler(message, BOT_INFO);
        } else {
            handler(message);
        }
        return true;
    }
    return false;
}

module.exports = {
    handleCommand,
    commandHandlers
};
