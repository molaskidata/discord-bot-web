const fs = require('fs');

const { getRandomResponse } = require('./utils');
const { EmbedBuilder } = require('discord.js');

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
    "Git commit -m 'fixed bug'"
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

const BIRTHDAY_FILE = 'birthdays.json';
function loadBirthdays() {
    if (fs.existsSync(BIRTHDAY_FILE)) {
        return JSON.parse(fs.readFileSync(BIRTHDAY_FILE));
    }
    return { channelId: null, users: {} };
}
function saveBirthdays(data) {
    fs.writeFileSync(BIRTHDAY_FILE, JSON.stringify(data, null, 2));
}

const commandHandlers = {
    '!birthdaychannel': async (message) => {
        await message.delete();
        const birthdays = loadBirthdays();
        birthdays.channelId = message.channel.id;
        saveBirthdays(birthdays);
        message.channel.send({
            content: `Set a Channel where I will send the birthday wishes. Format (<#${message.channel.id}>) The channel must be in the server where the command was sent. And the bot must have access to write and send messages.`,
            ephemeral: true
        });
    },
    '!birthdayset': async (message) => {
        await message.delete();
        message.channel.send({
            content: 'Write your birthday date in this format (dd/mm/yyyy) and click enter. The bot will save it for you.',
            ephemeral: true
        });
        const filter = m => m.author.id === message.author.id && /^\d{2}\/\d{2}\/\d{4}$/.test(m.content);
        const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
        collector.on('collect', m => {
            const birthdays = loadBirthdays();
            birthdays.users[m.author.id] = m.content;
            saveBirthdays(birthdays);
            m.channel.send({ content: 'Your birthday has been saved!', ephemeral: true });
            m.delete();
        });
    },
    '!hi': (message) => message.reply(getRandomResponse(hiResponses)),
    '!github': (message) => message.reply("Check that out my friend! `https://github.com/molaskidata`"),
    '!coffee': (message) => message.reply(getRandomResponse(coffeeResponses)),
    '!meme': (message) => message.reply(getRandomResponse(programmingMemes)),
    '!motivation': (message) => message.reply(getRandomResponse(motivationQuotes)),
    '!gg': (message) => message.reply("GG WP! 🎉"),
    '!goodnight': (message) => message.reply(getRandomResponse(goodnightResponses)),
    '!help': (message) => {
        const embed = new EmbedBuilder()
            .setColor('#168aad')
            .setTitle('Bot Command Help')
            .setDescription('Hier sind alle verfügbaren Commands:')
            .addFields(
                { name: 'Allgemein', value:
                    '`!hi` - Say hello\n' +
                    '`!coffee` - Time for coffee!\n' +
                    '`!meme` - Programming memes\n' +
                    '`!motivation` - Get motivated\n' +
                    '`!goodnight` - Good night messages\n' +
                    '`!ping` - Test bot\n' +
                    '`!info` - Bot info', inline: false },
                { name: 'GitHub', value:
                    '`!github` - Bot owner GitHub and repo\n' +
                    '`!congithubacc` - Connect your GitHub account\n' +
                    '`!discongithubacc` - Disconnect your GitHub account\n' +
                    '`!gitrank` - Show your GitHub commit level\n' +
                    '`!gitleader` - Show the top 10 committers', inline: false },
                { name: 'Birthday', value:
                    '`!birthdaychannel` - Set the birthday channel\n' +
                    '`!birthdayset` - Save your birthday', inline: false }
            )
            .setFooter({ text: 'Powered by CoderMaster', iconURL: undefined });
        message.reply({ embeds: [embed] });
    },
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
        const loginUrl = `https://thecoffeylounge.com/github-connect.html?discordId=${discordId}`;
        message.reply(
            `To connect your GitHub account, click this link: ${loginUrl}\n` +
            'Authorize the app, then return to Discord!'
        );
    },
    '!discongithubacc': async (message) => {
        const discordId = message.author.id;
        let data = {};
        if (fs.existsSync('github_links.json')) {
            data = JSON.parse(fs.readFileSync('github_links.json'));
        }
        if (data[discordId]) {
            delete data[discordId];
            fs.writeFileSync('github_links.json', JSON.stringify(data, null, 2));
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

setInterval(() => {
    const birthdays = loadBirthdays();
    if (!birthdays.channelId) return;
    const today = new Date();
    const todayStr = `${String(today.getDate()).padStart(2, '0')}/${String(today.getMonth()+1).padStart(2, '0')}`;
    Object.entries(birthdays.users).forEach(([userId, dateStr]) => {
        const [day, month, year] = dateStr.split('/');
        if (`${day}/${month}` === todayStr) {
            const channel = global.client.channels.cache.get(birthdays.channelId);
            if (channel) {
                channel.send(`Happy Birthdayyyy <@${userId}> ! We wish you only the best!`);
            }
        }
    });
}, 60 * 60 * 1000);

function handleCommand(message, BOT_INFO) {
    const handler = commandHandlers[message.content];
    if (handler) {
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
