const fs = require('fs');
const axios = require('axios');

const { getRandomResponse } = require('./utils');
const { EmbedBuilder } = require('discord.js');

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

const goodmorningResponses = [
    "Out of bed already? ☀️",
    "No coffee yet? ☕",
    "Good morning! Ready to crush some code today? 💻",
    "Rise and shine, developer! Time to debug the world! 🌍",
    "Morning! Let's make today bug-free! 🐛",
    "Good morning! Fresh day, fresh code! ✨"
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

const DISBOARD_BOT_ID = '302050872383242240';
let bumpReminders = new Map();

function setBumpReminder(channel, guild) {
    const channelId = channel.id;
    
    if (bumpReminders.has(channelId)) {
        clearTimeout(bumpReminders.get(channelId));
    }
    
    const reminderTimeout = setTimeout(() => {
        channel.send('⏰ **Bump Reminder!** ⏰\n\nThe server can be bumped again now! Use `/bump` to bump the server on Disboard! 🚀');
        bumpReminders.delete(channelId);
    }, 2 * 60 * 60 * 1000);
    
    bumpReminders.set(channelId, reminderTimeout);
    channel.send('✅ Bump reminder set! I\'ll remind you in 2 hours when the next bump is available.');
}

const commandHandlers = {
    '!birthdaychannel': async (message) => {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This command can only be used by administrators!');
            return;
        }
        const birthdays = loadBirthdays();
        birthdays.channelId = message.channel.id;
        saveBirthdays(birthdays);
        message.channel.send(
            `✅ Birthday channel set to <#${message.channel.id}>! I will send birthday wishes here. Make sure I have permission to write in this channel.`
        );
    },
    '!birthdayset': async (message) => {
        message.channel.send('Write your birthday date in this format (dd/mm/yyyy) and click enter. The bot will save it for you.');
        const filter = m => m.author.id === message.author.id && /^\d{2}\/\d{2}\/\d{4}$/.test(m.content);
        const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
        collector.on('collect', m => {
            const birthdays = loadBirthdays();
            birthdays.users[m.author.id] = m.content;
            saveBirthdays(birthdays);
            m.channel.send('Your birthday has been saved!');
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
            
            const twitchLinks = loadTwitchLinks();
            if (!twitchLinks[guildId]) twitchLinks[guildId] = {};
            
            const existingData = twitchLinks[guildId][userId];
            
            if (existingData) {
                const existingChannel = message.guild.channels.cache.get(existingData.clipChannelId);
                
                const embed = new EmbedBuilder()
                    .setColor('#7924BF')
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
                    }
                });
                
                choiceCollector.on('end', (collected) => {
                    if (collected.size === 0) {
                        message.channel.send('Setup timed out. Please try again with `!settwitch`.');
                    } else if (collected.first().content.toLowerCase() !== 'new') {
                        return;
                    }
                });
                
                await new Promise(resolve => {
                    choiceCollector.on('end', (collected) => {
                        if (collected.size > 0 && collected.first().content.toLowerCase() === 'new') {
                            resolve();
                        }
                    });
                    setTimeout(resolve, 61000);
                });
                
                const lastChoice = choiceCollector.collected.first();
                if (!lastChoice || lastChoice.content.toLowerCase() !== 'new') {
                    return;
                }
            }
            
            await new Promise(resolve => setTimeout(resolve, 2000));
            
            message.channel.send('Successfully connected to Discord.');
            message.channel.send('Set your Channel where I should send the Clips in. Format <#example> or use `!setchannel` to create a new one.');
            
            const channelFilter = msg => msg.author.id === userId && (msg.content.startsWith('<#') || msg.content === '!setchannel');
            const channelCollector = message.channel.createMessageCollector({ filter: channelFilter, time: 60000, max: 1 });
            
            channelCollector.on('collect', async (channelMsg) => {
                let clipChannelId;
                
                if (channelMsg.content === '!setchannel') {
                    try {
                        const newChannel = await message.guild.channels.create({
                            name: `${twitchUsername}-clips`,
                            type: 15,
                            permissionOverwrites: [
                                {
                                    id: message.guild.id,
                                    deny: ['SendMessages', 'CreatePublicThreads', 'CreatePrivateThreads', 'SendMessagesInThreads'],
                                    allow: ['ViewChannel', 'ReadMessageHistory']
                                },
                                {
                                    id: message.client.user.id,
                                    allow: ['SendMessages', 'CreatePublicThreads', 'SendMessagesInThreads', 'ViewChannel', 'ReadMessageHistory', 'ManageThreads']
                                }
                            ]
                        });
                        
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
                    const match = channelMsg.content.match(/<#(\d+)>/);
                    if (!match) {
                        message.channel.send('Invalid channel format. Please use <#channel> or !setchannel.');
                        return;
                    }
                    clipChannelId = match[1];
                    
                    const channel = message.guild.channels.cache.get(clipChannelId);
                    if (!channel) {
                        message.channel.send('Channel not found in this server. Please provide a valid channel.');
                        return;
                    }
                }
                
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
    '!testingtwitch': async (message) => {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        
        const guildId = message.guild.id;
        const userId = message.author.id;
        
        const twitchLinks = loadTwitchLinks();
        if (!twitchLinks[guildId] || !twitchLinks[guildId][userId]) {
            message.reply('❌ You don\'t have a Twitch account linked. Use `!settwitch` first.');
            return;
        }
        
        const userData = twitchLinks[guildId][userId];
        const twitchUsername = userData.twitchUsername;
        const clipChannelId = userData.clipChannelId;
        
        const clipChannel = message.guild.channels.cache.get(clipChannelId);
        if (!clipChannel) {
            message.reply('❌ Clip channel not found. Please run `!settwitch` again.');
            return;
        }
        
        message.channel.send(`🔍 Fetching latest clip from Twitch for **${twitchUsername}**...`);
        
        try {
            if (!process.env.TWITCH_CLIENT_ID || !process.env.TWITCH_CLIENT_SECRET) {
                message.reply('❌ Twitch API credentials not configured. Please add TWITCH_CLIENT_ID and TWITCH_CLIENT_SECRET to .env file.');
                return;
            }
            
            const tokenResponse = await axios.post('https://id.twitch.tv/oauth2/token', null, {
                params: {
                    client_id: process.env.TWITCH_CLIENT_ID,
                    client_secret: process.env.TWITCH_CLIENT_SECRET,
                    grant_type: 'client_credentials'
                }
            });
            
            if (!tokenResponse.data || !tokenResponse.data.access_token) {
                message.reply('❌ Failed to get Twitch access token. Please verify your Twitch API credentials.');
                console.error('Twitch token response:', tokenResponse.data);
                return;
            }
            
            const accessToken = tokenResponse.data.access_token;
            
            const userResponse = await axios.get('https://api.twitch.tv/helix/users', {
                params: { login: twitchUsername },
                headers: {
                    'Client-ID': process.env.TWITCH_CLIENT_ID || 'your_client_id',
                    'Authorization': `Bearer ${accessToken}`
                }
            });
            
            if (!userResponse.data.data || userResponse.data.data.length === 0) {
                message.reply(`❌ Twitch user **${twitchUsername}** not found.`);
                return;
            }
            
            const broadcasterId = userResponse.data.data[0].id;
            
            const clipsResponse = await axios.get('https://api.twitch.tv/helix/clips', {
                params: {
                    broadcaster_id: broadcasterId,
                    first: 1
                },
                headers: {
                    'Client-ID': process.env.TWITCH_CLIENT_ID || 'your_client_id',
                    'Authorization': `Bearer ${accessToken}`
                }
            });
            
            if (clipsResponse.data.data && clipsResponse.data.data.length > 0) {
                const clip = clipsResponse.data.data[0];
                const clipUrl = clip.url;
                
                const embed = new EmbedBuilder()
                    .setColor('#9146FF')
                    .setTitle(`🎬 Latest Clip: ${clip.title}`)
                    .setURL(clipUrl)
                    .setDescription(`Clipped by: ${clip.creator_name}`)
                    .addFields(
                        { name: 'Views', value: clip.view_count.toString(), inline: true },
                        { name: 'Created', value: new Date(clip.created_at).toLocaleDateString(), inline: true }
                    )
                    .setImage(clip.thumbnail_url)
                    .setFooter({ text: `Streamer: ${twitchUsername}` });
                
                if (clipChannel.type === 15) {
                    const thread = await clipChannel.threads.create({
                        name: `${clip.title.substring(0, 50)}`,
                        message: { content: clipUrl, embeds: [embed] }
                    });
                    message.channel.send(`✅ Test successful! Clip posted in thread: <#${thread.id}>`);
                } else {
                    await clipChannel.send({ content: clipUrl, embeds: [embed] });
                    message.channel.send(`✅ Test successful! Latest Twitch clip posted in <#${clipChannelId}>`);
                }
            } else {
                message.reply(`❌ No clips found for **${twitchUsername}** on Twitch. The channel may not have any clips yet.`);
            }
        } catch (error) {
            console.error('Twitch API error:', error);
            message.reply(`❌ Failed to fetch clips from Twitch. Error: ${error.response?.data?.message || error.message}`);
        }
    },
    '!hi': (message) => message.reply(getRandomResponse(hiResponses)),
    '!github': (message) => message.reply("Check that out my friend! `https://github.com/molaskidata`"),
    '!coffee': (message) => message.reply(getRandomResponse(coffeeResponses)),
    '!devmeme': (message) => message.reply(getRandomResponse(programmingMemes)),
    '!meme': (message) => message.reply(getRandomResponse(programmingMemes)),
    '!mot': (message) => message.reply(getRandomResponse(motivationQuotes)),
    '!motivation': (message) => message.reply(getRandomResponse(motivationQuotes)),
    '!gg': (message) => message.reply("GG WP! 🎉"),
    '!gn': (message) => message.reply(getRandomResponse(goodnightResponses)),
    '!gm': (message) => message.reply(getRandomResponse(goodmorningResponses)),
    '!setbumpreminder': (message) => {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        setBumpReminder(message.channel, message.guild);
    },
    '!bumpstatus': (message) => {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        if (bumpReminders.has(message.channel.id)) {
            message.channel.send('⏳ Bump reminder is active for this channel. You\'ll be notified when the next bump is available.');
        } else {
            message.channel.send('❌ No active bump reminder for this channel. Use `!setbumpreminder` to set one manually.');
        }
    },
    '!bumphelp': (message) => {
        message.channel.send(
            '**🤖 Bump Reminder System Help**\n\n' +
            '**Automatic Detection:** I automatically detect when you use `/bump` and set a 2-hour reminder!\n\n' +
            '**Manual Commands:**\n' +
            '`!setbumpreminder` - Manually set a 2-hour bump reminder *(admin only)*\n' +
            '`!bumpstatus` - Check if there\'s an active reminder for this channel *(admin only)*\n' +
            '`!bumphelp` - Show this help message\n\n' +
            '**How it works:** After a successful bump, I\'ll remind you exactly when the next bump is available (2 hours later)! 🚀'
        );
    },
    '!help': (message) => {
        const embed = new EmbedBuilder()
            .setColor('#1E1EC7')
            .setTitle('Bot Command Help')
            .setDescription('Hier sind alle verfügbaren Commands:')
            .addFields(
                { name: 'Allgemein', value:
                    '`!info` - Bot info\n' +
                    '`!ping` - Testing the pingspeed of the bot\n' +
                    '`!mot` - Get motivated for the day\n' +
                    '`!gm` - Good morning messages for you and your mates\n' +
                    '`!gn` - Good night messages for you and your mates\n' +
                    '`!hi` - Say hello and get an hello of me\n' +
                    '`!coffee` - Tell your friends it\'s coffee time!\n' +
                    '`!devmeme` - Let me give you a programming meme\n', inline: false },
                { name: 'GitHub', value:
                    '`!github` - Bot owners GitHub and Repos\n' +
                    '`!congithubacc` - Connect your GitHub account with the bot\n' +
                    '`!discongithubacc` - Disconnect your GitHub account\n' +
                    '`!gitrank` - Show your GitHub commit level\n' +
                    '`!gitleader` - Show the top 10 committers', inline: false },
                { name: 'Birthday', value:
                    '`!birthdaychannel` - Set the birthday channel *- only admin*\n' +
                    '`!birthdayset` - Save your birthday', inline: false },
                { name: 'Twitch *- only admin*', value:
                    '`!settwitch` - Link Twitch account and configure clip notifications *- only admin*\n' +
                    '`!setchannel` - Create a new thread-only channel for clips \n' +
                    '`(use during !settwitch setup)` *- only admin*\n' +
                    '`!testingtwitch` - Test clip posting by fetching latest clip *- only admin*', inline: false },
                { name: 'Bump Reminders', value:
                    '`!setbumpreminder` - Set 2-hour bump reminder *- only admin*\n' +
                    '`!bumpstatus` - Check bump reminder status *- only admin*\n' +
                    '`!bumphelp` - Show bump system help', inline: false }
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
            '`!hi`, `!coffee`, `!devmeme`, `!mot`, `!gn`, `!gm`, `!ping`, `!info`, `!github`'
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
    commandHandlers,
    setBumpReminder
};
