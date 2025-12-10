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

const TWITCH_FILE = 'twitch_links.json';
function loadTwitchLinks() {
    if (fs.existsSync(TWITCH_FILE)) {
        return JSON.parse(fs.readFileSync(TWITCH_FILE));
    }
    return {};
}
function saveTwitchLinks(data) {
    fs.writeFileSync(TWITCH_FILE, JSON.stringify(data, null, 2));
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
    '!settwitch': async (message) => {
        const guildId = message.guild.id;
        const userId = message.author.id;
        
        message.channel.send('Write your correct Twitch username in the Channel to synchronize and connect with Discord. Format: example');
        
        const usernameFilter = m => m.author.id === userId && !m.content.startsWith('!');
        const usernameCollector = message.channel.createMessageCollector({ filter: usernameFilter, time: 60000, max: 1 });
        
        usernameCollector.on('collect', async (m) => {
            const twitchUsername = m.content.trim();
            await m.delete();
            
            // Check if THIS USER already has a Twitch username saved
            const twitchLinks = loadTwitchLinks();
            if (!twitchLinks[guildId]) twitchLinks[guildId] = {};
            
            // Check if current user already has a saved configuration
            const existingData = twitchLinks[guildId][userId];
            
            if (existingData) {
                const existingChannel = message.guild.channels.cache.get(existingData.clipChannelId);
                
                const embed = new EmbedBuilder()
                    .setColor('#9b59b6')
                    .setTitle('⚠️ You Already Have a Twitch Account Linked')
                    .setDescription(`You already have a Twitch configuration saved.`)
                    .addFields(
                        { name: 'Current Username', value: existingData.twitchUsername, inline: true },
                        { name: 'Clip Channel', value: existingChannel ? `<#${existingData.clipChannelId}>` : 'Channel not found', inline: true }
                    )
                    .setFooter({ text: 'Y = View Stats | N = Cancel | New = Reset & Start Over' });
                
                message.channel.send({ embeds: [embed] });
                message.channel.send('Type **Y** to view stats, **N** to cancel, or **New** to delete and restart setup.');
                
                const choiceFilter = msg => msg.author.id === userId && ['y', 'n', 'new'].includes(msg.content.toLowerCase());
                const choiceCollector = message.channel.createMessageCollector({ filter: choiceFilter, time: 60000, max: 1 });
                
                choiceCollector.on('collect', async (choiceMsg) => {
                    const choice = choiceMsg.content.toLowerCase();
                    await choiceMsg.delete();
                    
                    if (choice === 'n') {
                        message.channel.send('Setup cancelled. The existing configuration remains unchanged.');
                        return;
                    }
                    
                    if (choice === 'y') {
                        const statsEmbed = new EmbedBuilder()
                            .setColor('#9b59b6')
                            .setTitle('📊 Your Twitch Connection Stats')
                            .addFields(
                                { name: 'Twitch Username', value: existingData.twitchUsername, inline: false },
                                { name: 'Clip Channel', value: existingChannel ? `<#${existingData.clipChannelId}>` : 'Channel deleted', inline: false }
                            )
                            .setTimestamp();
                        message.channel.send({ embeds: [statsEmbed] });
                        return;
                    }
                    
                    if (choice === 'new') {
                        delete twitchLinks[guildId][userId];
                        saveTwitchLinks(twitchLinks);
                        message.channel.send('Previous configuration deleted. Starting fresh setup...');
                        // Continue with new setup below
                    }
                });
                
                choiceCollector.on('end', (collected) => {
                    if (collected.size === 0) {
                        message.channel.send('Setup timed out. Please try again with `!settwitch`.');
                    } else if (collected.first().content.toLowerCase() !== 'new') {
                        return; // Stop if not 'new'
                    }
                });
                
                // Wait for choice collector to finish before continuing
                await new Promise(resolve => {
                    choiceCollector.on('end', (collected) => {
                        if (collected.size > 0 && collected.first().content.toLowerCase() === 'new') {
                            resolve();
                        }
                    });
                    setTimeout(resolve, 61000); // Timeout fallback
                });
                
                // If not 'new', stop here
                const lastChoice = choiceCollector.collected.first();
                if (!lastChoice || lastChoice.content.toLowerCase() !== 'new') {
                    return;
                }
            }
            
            // Simulate validation delay (max 5 seconds)
            await new Promise(resolve => setTimeout(resolve, 2000));
            
            message.channel.send('Successfully connected to Discord.');
            message.channel.send('Set your Channel where I should send the Clips in. Format <#example> or use `!setchannel` to create a new one.');
            
            const channelFilter = msg => msg.author.id === userId && (msg.content.startsWith('<#') || msg.content === '!setchannel');
            const channelCollector = message.channel.createMessageCollector({ filter: channelFilter, time: 60000, max: 1 });
            
            channelCollector.on('collect', async (channelMsg) => {
                await channelMsg.delete();
                let clipChannelId;
                
                if (channelMsg.content === '!setchannel') {
                    try {
                        // Create forum/thread channel - only bot can post threads
                        const newChannel = await message.guild.channels.create({
                            name: `${twitchUsername}-clips`,
                            type: 15, // GuildForum (Thread-only channel)
                            permissionOverwrites: [
                                {
                                    id: message.guild.id, // @everyone
                                    deny: ['SendMessages', 'CreatePublicThreads', 'CreatePrivateThreads', 'SendMessagesInThreads'],
                                    allow: ['ViewChannel', 'ReadMessageHistory']
                                },
                                {
                                    id: message.client.user.id, // Bot
                                    allow: ['SendMessages', 'CreatePublicThreads', 'SendMessagesInThreads', 'ViewChannel', 'ReadMessageHistory', 'ManageThreads']
                                }
                            ]
                        });
                        
                        // Add admin permissions
                        const adminRole = message.guild.roles.cache.find(r => r.permissions.has('Administrator'));
                        if (adminRole) {
                            await newChannel.permissionOverwrites.create(adminRole, { SendMessages: true });
                        }
                        
                        clipChannelId = newChannel.id;
                        message.channel.send(`Created new channel <#${clipChannelId}> for your clips!`);
                    } catch (err) {
                        message.channel.send('Failed to create channel. Please provide an existing channel instead.');
                        return;
                    }
                } else {
                    // Extract channel ID from <#channelId>
                    const match = channelMsg.content.match(/<#(\d+)>/);
                    if (!match) {
                        message.channel.send('Invalid channel format. Please use <#channel> or !setchannel.');
                        return;
                    }
                    clipChannelId = match[1];
                    
                    // Verify channel exists in guild
                    const channel = message.guild.channels.cache.get(clipChannelId);
                    if (!channel) {
                        message.channel.send('Channel not found in this server. Please provide a valid channel.');
                        return;
                    }
                }
                
                // Save to twitch_links.json
                await new Promise(resolve => setTimeout(resolve, 1000));
                const twitchLinks = loadTwitchLinks();
                if (!twitchLinks[guildId]) twitchLinks[guildId] = {};
                twitchLinks[guildId][userId] = {
                    twitchUsername,
                    clipChannelId
                };
                saveTwitchLinks(twitchLinks);
                
                const channelName = message.guild.channels.cache.get(clipChannelId)?.name || 'your channel';
                message.channel.send(`Your clips will be now sent in the #${channelName} channel. Have fun ${twitchUsername}.`);
            });
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
                    '`!birthdayset` - Save your birthday', inline: false },
                { name: 'Twitch', value:
                    '`!settwitch` - Connect your Twitch account and set clip channel', inline: false }
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
