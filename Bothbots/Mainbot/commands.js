    // ...existing code...
// Füge die Help-Handler INSIDE des commandHandlers-Objekts ein:
// (Suche nach const commandHandlers = { ... und füge sie dort ein)
// --- Security System Word Lists (multi-language, extend as needed) ---
const securityWordLists = [
    // German (provided)
    'anal','anus','arsch','boobs','cl1t','clit','dick','dickpic','fick','ficki','ficks','fuck','fucking','hure','huren','hurens','kitzler','milf','nackt','nacktbilder','nippel','nud3','nude','nudes','nutt','p0rn','p0rno','p3nis','penis','porn','porno','puss','pussy','s3x','scheide','schlampe','sex','sexual','slut','slutti','t1tt','titt','titten','vag1na','vagina',
    'arschloch','asozial','bastard','behindert','depp','dödel','dumm','dummi','hund','hundesohn','idiot','lappen','lappi','opfa','opfer','sohnedings','sohnemann','sohns','spast','spasti','wichser','wix','wixx','wixxer',
    'geh sterben','gehsterben','go die','ich bring dich um','ich töte dich','kill yourself','killyourself','kys','self harm','selfharm','sterb','suizid','töd dich','töt dich','verreck','verreckt','cl1ck','click here','discordgift','free nitro','freenitro','gift you nitro','steamgift','abschlacht','abschlachten','abst3chen','abstechen','abstich','angreifen','att4ck','attack','attackieren','aufhaengen','aufhängen','ausloeschen','auslöschen','ausradieren','bedroh','bedrohe','bedrohen','blut','brechdirdieknochen','bring dich um','bringdichum','bringmichum','erdrücken','erdruecken','erhaengen','erhängen','ermorden','erschies','erschießen','erstech','erstechen','erwuergen','erwürg','erwürgen','gefährd','gefährlich','k1ll','kill','kille','killer','knochenbrechen','m0rd','m4ssaker','massaker','mord','morden','pruegeln','prügeln','schiess','schieß','schlagdich','schlagmich','shoot','stech','stich','toeten','töten','umbr1ng','umbracht','umbringen',
    // English (partial, extend as needed)
    'anal','anus','ass','boobs','clit','dick','dickpic','fuck','fucking','whore','milf','nude','nudes','nipple','porn','porno','pussy','sex','slut','tits','vagina','bastard','idiot','dumb','stupid','retard','spastic','wanker','go die','kill yourself','kys','suicide','self harm','selfharm','die','murder','kill','attack','blood','shoot','stab','hang','dangerous','massacre','threat','gift nitro','free nitro','discordgift','click here','steamgift',
    // Add more: Danish, Serbisch, Kroatisch, Russisch, Finnisch, Italienisch, Spanisch
];

// --- Security Moderation Handler ---
async function handleSecurityModeration(message) {
    if (!message.guild) return;
    const guildId = message.guild.id;
    if (!isSecurityEnabledMain(guildId) && !securitySystemEnabled[guildId]) return;
    if (isOwnerOrAdmin(message.member)) return; // Don't moderate admins/owners

    const content = (message.content || '').toLowerCase();
    async function report(reason, matched) {
        await sendSecurityLogMain(message, reason, matched);
        await timeoutAndWarn(message, reason);
    }

    const inviteRegex = /(discord\.gg\/|discordapp\.com\/invite\/|discord\.com\/invite\/)/i;
    if (inviteRegex.test(content)) { await report('Invite links are not allowed!', 'invite link'); return; }
    if (/([a-zA-Z0-9])\1{6,}/.test(content) || /(.)\s*\1{6,}/.test(content)) { await report('Spam detected!', 'spam'); return; }
    for (const word of securityWordLists) {
        if (content.includes(word)) { await report(`Inappropriate language detected: "${word}"`, word); return; }
    }
    if (message.attachments && message.attachments.size > 0) {
        for (const [, attachment] of message.attachments) {
            const name = (attachment.name || '').toLowerCase();
            if (name.match(/(nude|nudes|porn|dick|boobs|sex|fuck|pussy|tits|vagina|penis|clit|anal|ass|nsfw|xxx|18\+|dickpic|nacktbilder|nackt|milf|slut|cum|cumshot|hure|huren|arsch|fick|titten|t1tt|nud3|p0rn|p0rno|p3nis|kitzler|scheide|schlampe|nutt|nippel)/)) { await report('NSFW/explicit image detected!', 'attachment: ' + name); return; }
        }
    }
}

const axios = require('axios');
const fs = require('fs');
const Groq = require('groq-sdk');

// --- Timeout and Warn Helper ---
async function timeoutAndWarn(message, reason) {
    try {
        try { await sendSecurityLogMain(message, reason); } catch (e) {}
        await message.delete();
        await message.member.timeout(2 * 60 * 60 * 1000, reason); // 2h timeout
        await message.author.send(`You have been timed out for 2 hours for: ${reason}`);
    } catch (err) {
        // Ignore errors (e.g. cannot DM user)
    }
}
// --- Security Moderation Listener ---
// Attach to your message event handler in your bot's main file:
// client.on('messageCreate', handleSecurityModeration);
// (fs already required above)

// Security system state (backwards compat)
const securitySystemEnabled = {};

// Persistent security config
const SECURITY_FILE = 'security_config.json';
let securityConfig = {};
function loadSecurityConfigMain() {
    if (fs.existsSync(SECURITY_FILE)) {
        try { return JSON.parse(fs.readFileSync(SECURITY_FILE)); } catch (e) { return {}; }
    }
    return {};
}
function saveSecurityConfigMain() { fs.writeFileSync(SECURITY_FILE, JSON.stringify(securityConfig, null, 2)); }
securityConfig = loadSecurityConfigMain();
function isSecurityEnabledMain(guildId) { return securityConfig[guildId] && securityConfig[guildId].enabled; }
// Store cleanup intervals per channel
const cleanupIntervals = {};
const CLEANUP_FILE = 'cleanup_intervals.json';

function loadCleanupIntervals() {
    if (fs.existsSync(CLEANUP_FILE)) {
        return JSON.parse(fs.readFileSync(CLEANUP_FILE));
    }
    return {};
}

function saveCleanupIntervals(data) {
    fs.writeFileSync(CLEANUP_FILE, JSON.stringify(data, null, 2));
}
const VERIFY_FILE = 'main_verify_config.json';
let verifyConfigMain = {};
function loadVerifyConfigMain() {
    if (fs.existsSync(VERIFY_FILE)) { try { return JSON.parse(fs.readFileSync(VERIFY_FILE)); } catch(e) { return {}; } }
    return {};
}
function saveVerifyConfigMain() { fs.writeFileSync(VERIFY_FILE, JSON.stringify(verifyConfigMain, null, 2)); }
verifyConfigMain = loadVerifyConfigMain();
const SERVER_LANG_FILE = 'server_languages.json';
function loadServerLanguages() {
    if (fs.existsSync(SERVER_LANG_FILE)) {
        return JSON.parse(fs.readFileSync(SERVER_LANG_FILE));
    }
    return {};
}
function saveServerLanguages(data) {
    fs.writeFileSync(SERVER_LANG_FILE, JSON.stringify(data, null, 2));
}

// --- Security logging helper (Mainbot) ---
async function sendSecurityLogMain(message, reason, matched="") {
    try {
        const guildId = message.guild ? message.guild.id : null;
        if (!guildId) return;
        const cfg = securityConfig[guildId];
        const channelId = cfg && cfg.logChannelId ? cfg.logChannelId : null;
        if (!channelId) return;
        const client = message.client;
        const ch = await client.channels.fetch(channelId).catch(()=>null);
        if (!ch) return;
        const { EmbedBuilder } = require('discord.js');
        const embed = new EmbedBuilder()
            .setTitle('Security Alert')
            .setColor('#ff7700')
            .addFields(
                { name: 'User', value: `${message.author.tag} (${message.author.id})`, inline: true },
                { name: 'Action', value: reason, inline: true },
                { name: 'Matched', value: matched || (message.content || '—'), inline: false }
            )
            .setTimestamp();
        let files = [];
        if (message.attachments && message.attachments.size>0) {
            for (const [,att] of message.attachments) { files.push(att.url); }
        }
        await ch.send({ content: `Security event in ${message.guild.name} (${message.guild.id})`, embeds: [embed], files: files });
    } catch (e) { /* ignore */ }
}


const { getRandomResponse } = require('./utils');
const { EmbedBuilder } = require('discord.js');
const { loadVoiceConfig, saveVoiceConfig, isPremiumUser, loadVoiceLogs } = require('./voiceSystem');

const BOT_OWNERS = [
    '1105877268775051316',
];

function isOwnerOrAdmin(member) {
    return BOT_OWNERS.includes(member.user.id) || member.permissions.has('Administrator');
}

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

const BUMP_FILE = 'bump_reminders.json';
function loadBumpReminders() {
    if (fs.existsSync(BUMP_FILE)) {
        return JSON.parse(fs.readFileSync(BUMP_FILE));
    }
    return {};
}
function saveBumpRemindersToFile(data) {
    fs.writeFileSync(BUMP_FILE, JSON.stringify(data, null, 2));
}

const DISBOARD_BOT_ID = '302050872383242240';
let bumpReminders = new Map();
// Maps helpdesk message ID -> origin channel ID (where !mungahelpdesk was executed)
const helpdeskOrigins = new Map();
// Maps support ticket message ID -> origin channel ID (where !munga-supportticket was executed)
const supportOrigins = new Map();
const TICKETS_CONFIG_FILE = 'tickets_config.json';
function loadTicketsConfig() {
    if (fs.existsSync(TICKETS_CONFIG_FILE)) {
        return JSON.parse(fs.readFileSync(TICKETS_CONFIG_FILE));
    }
    return {};
}
function saveTicketsConfig(data) {
    fs.writeFileSync(TICKETS_CONFIG_FILE, JSON.stringify(data, null, 2));
}

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

function restoreBumpReminders(client) {
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

// Restore cleanup intervals from disk and reschedule them
function restoreCleanupIntervals(client) {
    const stored = loadCleanupIntervals();
    const now = Date.now();
    Object.entries(stored).forEach(([channelId, data]) => {
        const timeLeft = (data.nextRun || now) - now;
        const chan = client.channels.cache.get(channelId);
        if (!chan) {
            // channel not available, remove stored entry
            delete stored[channelId];
            saveCleanupIntervals(stored);
            return;
        }

        // schedule first run after timeLeft (or immediate if negative)
        const firstDelay = Math.max(0, timeLeft);
        setTimeout(async () => {
            // perform cleanup then set interval
            try {
                // reuse doFullCleanup from above by requiring the module's scope
                // but doFullCleanup is defined inside !cleanup scope; recreate minimal cleanup logic here
                let deleted = 0;
                let lastId = null;
                while (true) {
                    const options = { limit: 100 };
                    if (lastId) options.before = lastId;
                    const messages = await chan.messages.fetch(options);
                    if (messages.size === 0) break;
                    for (const msg of messages.values()) {
                        try { await msg.delete(); deleted++; } catch (e) { }
                    }
                    lastId = messages.last().id;
                    if (messages.size < 100) break;
                }
                try { await chan.send(`🧹 Cleanup complete! **${deleted}** messages deleted.`); } catch (e) { }
            } catch (e) { try { await chan.send('❌ Error while deleting messages.'); } catch (e) { } }

            // schedule hourly interval
            const interval = setInterval(async () => {
                let deleted = 0;
                let lastId = null;
                try {
                    while (true) {
                        const options = { limit: 100 };
                        if (lastId) options.before = lastId;
                        const messages = await chan.messages.fetch(options);
                        if (messages.size === 0) break;
                        for (const msg of messages.values()) {
                            try { await msg.delete(); deleted++; } catch (e) { }
                        }
                        lastId = messages.last().id;
                        if (messages.size < 100) break;
                    }
                    try { await chan.send(`🧹 Cleanup complete! **${deleted}** messages deleted.`); } catch (e) { }
                } catch (err) { try { await chan.send('❌ Error while deleting messages.'); } catch (e) { } }
                try {
                    const s = loadCleanupIntervals();
                    s[channelId] = { nextRun: Date.now() + 60 * 60 * 1000 };
                    saveCleanupIntervals(s);
                } catch (e) { }
            }, 60 * 60 * 1000);
            cleanupIntervals[channelId] = interval;
            // persist nextRun
            try {
                const s = loadCleanupIntervals();
                s[channelId] = { nextRun: Date.now() + 60 * 60 * 1000 };
                saveCleanupIntervals(s);
            } catch (e) { }
        }, firstDelay);
    });
}

const commandHandlers = {
                                        // (removed) duplicate help command handled by !mungahelpdesk
                                    '!setupflirtlang': (message) => {
                                        if (!isOwnerOrAdmin(message.member)) {
                                            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
                                            return;
                                        }
                                        const args = message.content.split(' ');
                                        if (args.length < 2) {
                                            message.reply('Usage: !setupflirtlang [language]\nExample: !setupflirtlang English');
                                            return;
                                        }
                                        const lang = args[1].toLowerCase();
                                        const serverId = message.guild.id;
                                        const langs = loadServerLanguages();
                                        langs[serverId] = lang;
                                        saveServerLanguages(langs);
                                        message.reply(`✅ Flirt language for this server set to **${lang}**. The AI will now use this language for flirts.`);
                                    },
                                    '!removeflirtlang': (message) => {
                                        if (!isOwnerOrAdmin(message.member)) {
                                            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
                                            return;
                                        }
                                        const serverId = message.guild.id;
                                        const langs = loadServerLanguages();
                                        if (langs[serverId]) {
                                            delete langs[serverId];
                                            saveServerLanguages(langs);
                                            message.reply('✅ Flirt language setting removed for this server. The AI will now auto-detect language for flirts.');
                                        } else {
                                            message.reply('No flirt language was set for this server.');
                                        }
                                    },
                                '!delbumpreminder': (message) => {
                                    if (!isOwnerOrAdmin(message.member)) {
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
                                },
                            '!mungahelpdesk': async (message) => {
                                // Public helpdesk poster (any user)
                                message.reply('Please send the target Channel ID where the helpdesk should be posted. (60 seconds)');
                                const filter = m => m.author.id === message.author.id && /^\d{17,20}$/.test(m.content.trim());
                                const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
                                collector.on('collect', async (msg) => {
                                    const channelId = msg.content.trim();
                                    const targetChannel = message.guild.channels.cache.get(channelId);
                                    if (!targetChannel) {
                                        message.reply('❌ Channel not found. Please check the ID.');
                                        return;
                                    }
                                    const { ActionRowBuilder, StringSelectMenuBuilder, EmbedBuilder } = require('discord.js');
                                    const helpEmbed = new EmbedBuilder()
                                        .setColor('#80c20eff')
                                        .setAuthor({ name: "!Code.Master()", iconURL: 'https://imgur.com/diztsFC.png' })
                                        .setTitle('Self Support / Bot Help')
                                        .setDescription('Choose a category to get help:')
                                        .addFields(
                                            { name: 'General Help', value: '• **!help** – Shows all commands' },
                                            { name: 'Voice Commands', value: '• **!helpyvoice** – Voice-specific help' },
                                            { name: 'Security Commands', value: '• **!helpysecure** – Moderation/Security help' },
                                            { name: 'Twitch Commands', value: '• **!helpytwitch** – Twitch integration help' },
                                            { name: 'GitHub Commands', value: '• **!helpygithub** – GitHub integration help' },
                                            { name: 'Bump Commands', value: '• **!helpybump** – Bump/Disboard help' },
                                            { name: 'Birthday Commands', value: '• **!helpybirth** – Birthday system help' }
                                        )
                                        .setImage('https://imgur.com/VYHroXP.png')
                                        .setFooter({ text: 'Choose a category using the menu below!' });

                                    const selectMenu = new StringSelectMenuBuilder()
                                        .setCustomId('helpdesk_select')
                                        .setPlaceholder('Choose a help category')
                                        .addOptions([
                                            { label: 'All Commands', description: 'Complete overview (!help)', value: 'help_all', emoji: '📖' },
                                            { label: 'Voice Commands', description: 'Voice system help', value: 'help_voice', emoji: '🎤' },
                                            { label: 'Security Commands', description: 'Moderation/Security help', value: 'help_secure', emoji: '🛡️' },
                                            { label: 'Twitch Commands', description: 'Twitch integration help', value: 'help_twitch', emoji: '📺' },
                                            { label: 'GitHub Commands', description: 'GitHub integration help', value: 'help_github', emoji: '🐙' },
                                            { label: 'Bump Commands', description: 'Bump/Disboard help', value: 'help_bump', emoji: '🔔' },
                                            { label: 'Birthday Commands', description: 'Birthday system help', value: 'help_birth', emoji: '🎂' }
                                        ]);
                                    const row = new ActionRowBuilder().addComponents(selectMenu);
                                    const sent = await targetChannel.send({ embeds: [helpEmbed], components: [row] });
                                    // remember which channel the user requested the helpdesk from
                                    try { helpdeskOrigins.set(sent.id, message.channel.id); } catch (e) { /* ignore */ }
                                    message.reply('✅ Helpdesk posted in the requested channel!');
                                });
                                collector.on('end', (collected) => {
                                    if (collected.size === 0) {
                                        message.reply('❌ Time expired. Please run the command again.');
                                    }
                                });
                            },
                            '!munga-supportticket': async (message) => {
                                // Support ticket poster (any user)
                                message.reply('Please send the target Channel ID where the support ticket embed should be posted. (60 seconds)');
                                const filter = m => m.author.id === message.author.id && /^\d{17,20}$/.test(m.content.trim());
                                const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
                                collector.on('collect', async (msg) => {
                                    const channelId = msg.content.trim();
                                    const targetChannel = message.guild.channels.cache.get(channelId);
                                    if (!targetChannel) {
                                        message.reply('❌ Channel not found. Please check the ID.');
                                        return;
                                    }
                                    const { ActionRowBuilder, StringSelectMenuBuilder, EmbedBuilder } = require('discord.js');

                                    const supportEmbed = new EmbedBuilder()
                                        .setColor('#2f3136')
                                        .setAuthor({ name: 'Support Ticket', iconURL: 'https://i.imgur.com/4I7tViC.png' })
                                        .setTitle('Create a Support Ticket')
                                        .setDescription('Need help? Choose the appropriate option from the menu below to create a support ticket. Provide as much detail as possible when prompted.\n\nExamples of what to report:\n• Technical issues (bot errors, command failures, crashes)\n• Server abuse or harassment (spam, targeted threats, moderation requests)\n• Scam or phishing attempts (suspicious links, impersonation)\n• Advertising / recruitment (unsolicited promotions or bot invites)\n• Bug reports & feature requests (steps to reproduce, expected behavior)\n• Other (billing, access, or custom requests)')
                                        .setImage('https://i.imgur.com/4I7tViC.png')
                                        .setFooter({ text: 'Select a category to start a ticket — a staff member will respond.' });

                                    const selectMenu = new StringSelectMenuBuilder()
                                        .setCustomId('support_select')
                                        .setPlaceholder('Choose a support category')
                                        .addOptions([
                                            { label: 'Technical Issue', description: 'Bot or server technical problem', value: 'support_technical', emoji: '🛠️' },
                                            { label: 'Spam / Scam', description: 'Report spam, phishing or scams', value: 'support_spam', emoji: '⚠️' },
                                            { label: 'Abuse / Harassment', description: 'Report abuse or threatening behavior', value: 'support_abuse', emoji: '🚨' },
                                            { label: 'Advertising / Recruitment', description: 'Unwanted promotions or invites', value: 'support_ad', emoji: '📣' },
                                            { label: 'Bug / Feature', description: 'Report a bug or request a feature', value: 'support_bug', emoji: '🐛' },
                                            { label: 'Other', description: 'Other support inquiries', value: 'support_other', emoji: '❓' }
                                        ]);

                                    const row = new ActionRowBuilder().addComponents(selectMenu);
                                    const sent = await targetChannel.send({ embeds: [supportEmbed], components: [row] });
                                    try { supportOrigins.set(sent.id, message.channel.id); } catch (e) { /* ignore */ }
                                    message.reply('✅ Support ticket embed posted in the requested channel!');
                                });
                                collector.on('end', (collected) => {
                                    if (collected.size === 0) {
                                        message.reply('❌ Time expired. Please run the command again.');
                                    }
                                });
                            },
                            '!munga-ticketsystem': async (message) => {
                                if (!isOwnerOrAdmin(message.member)) {
                                    message.reply('❌ Admin only command.');
                                    return;
                                }
                                message.reply('Send the Log Channel ID where ticket transcripts should be posted, or type **!create** to let the bot create a new log channel for admins. (60s)');
                                const filter = m => m.author.id === message.author.id;
                                const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
                                collector.on('collect', async (m) => {
                                    const val = m.content.trim();
                                    let logChannelId = null;
                                    if (val.toLowerCase() === '!create') {
                                        try {
                                            const created = await message.guild.channels.create({
                                                name: 'tickets-log',
                                                type: 0,
                                                permissionOverwrites: [
                                                    { id: message.guild.id, deny: ['ViewChannel'] }
                                                ]
                                            });
                                            // allow admins to view
                                            message.guild.roles.cache.filter(r => r.permissions.has('Administrator')).forEach(role => {
                                                created.permissionOverwrites.create(role, { ViewChannel: true, SendMessages: true, ReadMessageHistory: true }).catch(() => {});
                                            });
                                            // allow bot
                                            created.permissionOverwrites.create(message.client.user.id, { ViewChannel: true, SendMessages: true, ReadMessageHistory: true }).catch(() => {});
                                            logChannelId = created.id;
                                        } catch (err) {
                                            message.reply('❌ Failed to create log channel. Provide an existing channel ID instead.');
                                            return;
                                        }
                                    } else {
                                        const match = val.match(/<#?(\d+)>?/);
                                        if (!match) { message.reply('❌ Invalid channel ID.'); return; }
                                        const chan = message.guild.channels.cache.get(match[1]);
                                        if (!chan) { message.reply('❌ Channel not found in this server.'); return; }
                                        logChannelId = match[1];
                                    }

                                    const cfg = loadTicketsConfig();
                                    cfg[message.guild.id] = { logChannelId };
                                    saveTicketsConfig(cfg);
                                    message.reply(`✅ Ticket system configured. Log channel: <#${logChannelId}>`);
                                });
                                collector.on('end', (collected) => { if (collected.size === 0) message.reply('❌ Time expired. Please run the command again.'); });
                            },
                        // (removed) duplicate helpdesk — use !mungahelpdesk

                    // Interaktion-Handler für das SelectMenu (in infobot.js oder mainbot.js einbauen!):
                    // client.on('interactionCreate', async interaction => {
                    //   if (!interaction.isStringSelectMenu()) return;
                    //   if (interaction.customId !== 'helpdesk_select') return;
                    //   let reply;
                    //   switch (interaction.values[0]) {
                    //     case 'help_all': reply = '...!help Embed oder Text...'; break;
                    //     case 'help_voice': reply = '...!helpyvoice Embed oder Text...'; break;
                    //     ...
                    //   }
                    //   await interaction.reply({ content: reply, ephemeral: true });
                    // });
                    '!helpyvoice': (message) => {
                        const embed = new EmbedBuilder()
                            .setColor('#11806a')
                            .setTitle('★ Voice Commands')
                            .setDescription('Alle Voice-spezifischen Befehle:')
                            .addFields(
                                { name: 'Voice', value:
                                    '`!setupvoice` - Join-to-Create Channel erstellen\n' +
                                    '`!setupvoicelog` - Voice Log Channel erstellen\n' +
                                    '`!cleanupvoice` - Voice Log Channel säubern\n' +
                                    '`!deletevoice` - Voice System löschen\n' +
                                    '`!voicename [name]` - Voice Channel umbenennen\n' +
                                    '`!voicelimit [0-99]` - Userlimit setzen\n' +
                                    '`!voicetemplate [gaming/study/chill]` - Template anwenden\n' +
                                    '`!voicelock/unlock` - Channel sperren/entsperren\n' +
                                    '`!voicekick @user` - User aus Channel kicken\n' +
                                    '`!voicestats` - Voice Aktivitätsstatistik\n' +
                                    '`!voiceprivate` - Channel privat machen\n' +
                                    '`!voicepermit @user` - User erlauben\n' +
                                    '`!voicedeny @user` - User blockieren', inline: false }
                            )
                            .setFooter({ text: 'Nur Voice Features' });
                        message.reply({ embeds: [embed] });
                    },
                    '!helpysecure': (message) => {
                        const embed = new EmbedBuilder()
                            .setColor('#e74c3c')
                            .setTitle('★ Security Commands')
                            .setDescription('Alle Security/Moderation Befehle:')
                            .addFields(
                                { name: 'Security', value:
                                    '`!setsecuritymod` - Security System aktivieren\n' +
                                    '`!sban @user` - User bannen\n' +
                                    '`!skick @user` - User kicken\n' +
                                    '`!stimeout @user [min]` - User Timeout\n' +
                                    '`!stimeoutdel @user` - Timeout entfernen', inline: false }
                            )
                            .setFooter({ text: 'Nur Security Features' });
                        message.reply({ embeds: [embed] });
                    },
                    '!helpytwitch': (message) => {
                        const embed = new EmbedBuilder()
                            .setColor('#9147ff')
                            .setTitle('★ Twitch Commands')
                            .setDescription('Alle Twitch-spezifischen Befehle:')
                            .addFields(
                                { name: 'Twitch', value:
                                    '`!settwitch` - Twitch Account verknüpfen\n' +
                                    '`!setchannel` - Clip Channel erstellen\n' +
                                    '`!testingtwitch` - Clip-Post Test\n' +
                                    '`!deletetwitch` - Twitch Account entfernen', inline: false }
                            )
                            .setFooter({ text: 'Nur Twitch Features' });
                        message.reply({ embeds: [embed] });
                    },
                    '!helpygithub': (message) => {
                        const embed = new EmbedBuilder()
                            .setColor('#24292e')
                            .setTitle('★ GitHub Commands')
                            .setDescription('Alle GitHub-spezifischen Befehle:')
                            .addFields(
                                { name: 'GitHub', value:
                                    '`!congithubacc` - GitHub Account verbinden\n' +
                                    '`!discongithubacc` - GitHub Account trennen', inline: false }
                            )
                            .setFooter({ text: 'Nur GitHub Features' });
                        message.reply({ embeds: [embed] });
                    },
                    '!helpybump': (message) => {
                        const embed = new EmbedBuilder()
                            .setColor('#f1c40f')
                            .setTitle('★ Bump/Disboard Befehle')
                            .setDescription('Alle verfügbaren Bump/Disboard Commands:')
                            .addFields(
                                { name: 'Bump Reminder', value:
                                    '`!setbumpreminder` - Setze einen 2-Stunden Bump-Reminder\n' +
                                    '`!delbumpreminder` - Lösche den aktiven Bump-Reminder\n' +
                                    '`!bumpreminder` - Aktiviere den Bump-Reminder (Alias)\n' +
                                    '`!bumpreminderdel` - Deaktiviere den Bump-Reminder (Alias)\n' +
                                    '`!bumpstatus` - Zeigt den Status des Bump-Reminders\n' +
                                    '`!bumphelp` - Zeigt Hilfe zum Bump-System', inline: false }
                            )
                            .setFooter({ text: 'Nur Bump Features' });
                        message.reply({ embeds: [embed] });
                    },
                    '!helpybirth': (message) => {
                        const embed = new EmbedBuilder()
                            .setColor('#ffb347')
                            .setTitle('★ Birthday Commands')
                            .setDescription('Alle Geburtstags-Befehle:')
                            .addFields(
                                { name: 'Birthday', value:
                                    '`!birthdaychannel` - Channel für Geburtstage setzen\n' +
                                    '`!birthdayset` - Geburtstag eintragen', inline: false }
                            )
                            .setFooter({ text: 'Nur Birthday Features' });
                        message.reply({ embeds: [embed] });
                    },
                '!sban': async (message) => {
                    if (!isOwnerOrAdmin(message.member) || !isPremiumUser(message.author.id)) {
                        message.reply('❌ This is an admin-only and premium command.');
                        return;
                    }
                    const user = message.mentions.users.first();
                    if (!user) {
                        message.reply('Usage: !sban @user');
                        return;
                    }
                    try {
                        await message.guild.members.ban(user.id, { reason: 'Manual security ban' });
                        message.reply(`🔨 Banned ${user.tag}`);
                    } catch (err) {
                        message.reply('❌ Failed to ban user.');
                    }
                },
                '!skick': async (message) => {
                    if (!isOwnerOrAdmin(message.member) || !isPremiumUser(message.author.id)) {
                        message.reply('❌ This is an admin-only and premium command.');
                        return;
                    }
                    const user = message.mentions.users.first();
                    if (!user) {
                        message.reply('Usage: !skick @user');
                        return;
                    }
                    try {
                        await message.guild.members.kick(user.id, 'Manual security kick');
                        message.reply(`👢 Kicked ${user.tag}`);
                    } catch (err) {
                        message.reply('❌ Failed to kick user.');
                    }
                },
                '!stimeout': async (message) => {
                    if (!isOwnerOrAdmin(message.member) || !isPremiumUser(message.author.id)) {
                        message.reply('❌ This is an admin-only and premium command.');
                        return;
                    }
                    const user = message.mentions.users.first();
                    const args = message.content.split(' ');
                    const duration = parseInt(args[2]) || 120; // default 120 min
                    if (!user) {
                        message.reply('Usage: !stimeout @user [minutes]');
                        return;
                    }
                    try {
                        const member = await message.guild.members.fetch(user.id);
                        await member.timeout(duration * 60 * 1000, 'Manual security timeout');
                        message.reply(`⏳ Timed out ${user.tag} for ${duration} minutes.`);
                    } catch (err) {
                        message.reply('❌ Failed to timeout user.');
                    }
                },
                '!stimeoutdel': async (message) => {
                    if (!isOwnerOrAdmin(message.member) || !isPremiumUser(message.author.id)) {
                        message.reply('❌ This is an admin-only and premium command.');
                        return;
                    }
                    const user = message.mentions.users.first();
                    if (!user) {
                        message.reply('Usage: !stimeoutdel @user');
                        return;
                    }
                    try {
                        const member = await message.guild.members.fetch(user.id);
                        await member.timeout(null, 'Manual security timeout removed');
                        message.reply(`✅ Timeout removed for ${user.tag}`);
                    } catch (err) {
                        message.reply('❌ Failed to remove timeout.');
                    }
                },
            '!setsecuritymod': async (message) => {
                if (!isOwnerOrAdmin(message.member)) {
                    message.reply('❌ This is an admin-only command.');
                    return;
                }
                const guildId = message.guild.id;
                if (isSecurityEnabledMain(guildId) || securitySystemEnabled[guildId]) {
                    message.reply('⚠️ Security system is already enabled for this server.');
                    return;
                }
                securityConfig[guildId] = securityConfig[guildId] || {};
                securityConfig[guildId].enabled = true;
                saveSecurityConfigMain();

                const step = await message.reply('🛡️ Security system has been enabled for this server! The bot will now monitor for spam, NSFW, invite links, and offensive language in all supported languages.\\n\\nPlease provide the CHANNEL ID where I should send the warn logs (type `none` to disable logging, or type `!setchannelsec` to let me create a warn-log channel for you).');

                const filter = (m) => m.author.id === message.author.id;
                const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
                collector.on('collect', async (m) => {
                    const val = (m.content || '').trim();
                    if (val.toLowerCase() === 'none') {
                        securityConfig[guildId].logChannelId = null;
                        saveSecurityConfigMain();
                        message.reply('✅ Security enabled with no logging. All right! Now lean back, I work now for you and yes. 24/7 baby ;))');
                        return;
                    }
                    if (val === '!setchannelsec' || val.toLowerCase() === 'create') {
                        try {
                            const ch = await message.guild.channels.create({ name: 'warn-logs', type: 0, permissionOverwrites: [{ id: message.guild.id, deny: ['ViewChannel'] }] });
                            securityConfig[guildId].logChannelId = ch.id;
                            saveSecurityConfigMain();
                            message.reply(`✅ Created and set warn log channel: ${ch}. All right! Now lean back, I work now for you and yes. 24/7 baby ;))`);
                        } catch (e) {
                            message.reply('❌ Failed to create log channel. Please provide a channel ID or create one and run the command again.');
                        }
                        return;
                    }
                    const maybeId = val.replace(/[^0-9]/g, '');
                    if (!maybeId) { message.reply('❌ Invalid input. Provide a channel ID, `none`, or `!setchannelsec`.'); return; }
                    const ch = await message.guild.channels.fetch(maybeId).catch(()=>null);
                    if (!ch) { message.reply('❌ Channel not found. Make sure I can access it and provide the numeric Channel ID.'); return; }
                    securityConfig[guildId].logChannelId = ch.id;
                    saveSecurityConfigMain();
                    message.reply(`✅ Warn log channel set to ${ch}. All right! Now lean back, I work now for you and yes. 24/7 baby ;))`);
                });
                collector.on('end', (collected) => {
                    if (collected.size === 0) {
                        message.reply('⌛ Timeout: no channel provided. You can run `!setsecuritymod` again to set logging.');
                    }
                });
            },
            // --- Verify system (mainbot) ---
            '!setverify': async (message) => {
                if (!isOwnerOrAdmin(message.member)) { message.reply('❌ Admins only'); return; }
                const guild = message.guild;
                const filter = (m) => m.author.id === message.author.id;
                await message.reply('Please provide the CHANNEL ID where users must verify (or type `cancel`).');
                const ccol = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
                ccol.on('collect', async (m1) => {
                    if (m1.content.toLowerCase() === 'cancel') { message.reply('Cancelled.'); return; }
                    const chanId = m1.content.replace(/[^0-9]/g,'');
                    const chan = await guild.channels.fetch(chanId).catch(()=>null);
                    if (!chan) { message.reply('❌ Channel not found or inaccessible. Aborting.'); return; }
                    await message.reply('Now provide the ROLE ID users should receive on verification (or `cancel`).');
                    const rcol = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
                    rcol.on('collect', async (m2) => {
                        if (m2.content.toLowerCase() === 'cancel') { message.reply('Cancelled.'); return; }
                        const roleId = m2.content.replace(/[^0-9]/g,'');
                        const role = await guild.roles.fetch(roleId).catch(()=>null);
                        if (!role) { message.reply('❌ Role not found. Aborting.'); return; }
                        verifyConfigMain[guild.id] = { channelId: chan.id, roleId: role.id, messageId: null };
                        saveVerifyConfigMain();
                        const errors = [];
                        for (const [, c] of guild.channels.cache) {
                            try {
                                if (!c.manageable) continue;
                                if (c.id === chan.id) {
                                    await c.permissionOverwrites.edit(guild.id, { ViewChannel: true });
                                } else {
                                    await c.permissionOverwrites.edit(guild.id, { ViewChannel: false });
                                }
                            } catch (e) { errors.push(c.id); }
                        }
                        try {
                            const { EmbedBuilder } = require('discord.js');
                            const embed = new EmbedBuilder()
                                .setTitle('Verify to access the server')
                                .setDescription('To verify and get access to the server, type `!verify` in this channel.')
                                .setColor('#2b6cb0')
                                .setFooter({ text: 'Verification — stay safe' });
                            const sent = await chan.send({ embeds: [embed] });
                            verifyConfigMain[guild.id].messageId = sent.id;
                            saveVerifyConfigMain();
                            message.reply(`✅ Verify configured in ${chan}. ${errors.length ? 'Some channels could not be updated due to permissions.' : ''}`);
                        } catch (e) {
                            message.reply('✅ Config saved, but failed to post verify message in the channel. Check my permissions.');
                        }
                    });
                    rcol.on('end', (col) => { if (col.size===0) message.reply('Timeout waiting for role ID.'); });
                });
                ccol.on('end', (col) => { if (col.size===0) message.reply('Timeout waiting for channel ID.'); });
            },

            '!verify': async (message) => {
                const cfg = verifyConfigMain[message.guild.id];
                if (!cfg || !cfg.channelId) { message.reply('Verification is not configured on this server.'); return; }
                if (message.channel.id !== cfg.channelId) { message.reply('Please verify in the verification channel.'); return; }
                const member = message.member;
                const role = await message.guild.roles.fetch(cfg.roleId).catch(()=>null);
                if (!role) { message.reply('Verification role no longer exists. Contact an admin.'); return; }
                try { await member.roles.add(role); message.reply('✅ You have been verified and the role was assigned. Welcome!'); }
                catch (e) { message.reply('❌ Failed to assign role. I may lack Manage Roles permission or the role is higher than my role.'); }
            },

            '!delverifysett': async (message) => {
                if (!isOwnerOrAdmin(message.member)) { message.reply('❌ Admins only'); return; }
                const cfg = verifyConfigMain[message.guild.id];
                if (!cfg) { message.reply('No verify setup found.'); return; }
                await message.reply('Are you sure? Type `Y` to confirm deletion, `N` to cancel (60s).');
                const filter = (m) => m.author.id === message.author.id;
                const col = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
                col.on('collect', async (m) => {
                    const v = m.content.trim().toLowerCase();
                    if (v === 'y' || v === 'yes') {
                        try { const ch = await message.guild.channels.fetch(cfg.channelId).catch(()=>null); if (ch && cfg.messageId) { const msg = await ch.messages.fetch(cfg.messageId).catch(()=>null); if (msg) await msg.delete().catch(()=>{}); } } catch(e){}
                        delete verifyConfigMain[message.guild.id]; saveVerifyConfigMain(); message.reply('✅ Verify setup removed.');
                    } else { message.reply('Aborted. No changes made.'); }
                });
                col.on('end', (c) => { if (c.size===0) message.reply('Timeout. No changes made.'); });
            },
            '!security': async (message) => {
                if (!isOwnerOrAdmin(message.member)) { message.reply('❌ Admins only'); return; }
                const parts = message.content.split(' ').filter(Boolean);
                const arg = parts[1] ? parts[1].toLowerCase() : null;
                const gid = message.guild.id;
                securityConfig[gid] = securityConfig[gid] || {};
                if (!arg || arg === 'status') {
                    const enabled = !!securityConfig[gid].enabled;
                    const logId = securityConfig[gid].logChannelId || 'none';
                    message.reply(`Security: ${enabled ? 'ENABLED' : 'disabled'}. Log channel: ${logId}`);
                    return;
                }
                if (arg === 'on' || arg === 'enable') { securityConfig[gid].enabled = true; saveSecurityConfigMain(); message.reply('✅ Security enabled for this server.'); return; }
                if (arg === 'off' || arg === 'disable') { securityConfig[gid].enabled = false; saveSecurityConfigMain(); message.reply('✅ Security disabled for this server.'); return; }
                message.reply('Usage: !security <on|off|status>');
            },
            '!setseclog': async (message) => {
                if (!isOwnerOrAdmin(message.member)) { message.reply('❌ Admins only'); return; }
                const parts = message.content.split(' ').filter(Boolean);
                const arg = parts[1] ? parts[1].trim() : null;
                const gid = message.guild.id;
                securityConfig[gid] = securityConfig[gid] || {};
                if (!arg) { message.reply('Usage: !setseclog <channelId|none|create>'); return; }
                if (arg.toLowerCase() === 'none') { securityConfig[gid].logChannelId = null; saveSecurityConfigMain(); message.reply('✅ Logging disabled for this server.'); return; }
                if (arg.toLowerCase() === 'create') {
                    try { const ch = await message.guild.channels.create({ name: 'warn-logs', type: 0, permissionOverwrites: [{ id: message.guild.id, deny: ['ViewChannel'] }] }); securityConfig[gid].logChannelId = ch.id; saveSecurityConfigMain(); message.reply(`✅ Created warn-log channel: ${ch}`); } catch (e) { message.reply('❌ Failed to create channel'); }
                    return;
                }
                const maybe = arg.replace(/[^0-9]/g,'');
                if (!maybe) { message.reply('❌ Invalid channel id'); return; }
                const ch = await message.guild.channels.fetch(maybe).catch(()=>null);
                if (!ch) { message.reply('❌ Channel not found'); return; }
                securityConfig[gid].logChannelId = ch.id; saveSecurityConfigMain(); message.reply(`✅ Log channel set to ${ch}`);
            },
        '!cleanup': async (message) => {
            if (!isOwnerOrAdmin(message.member)) {
                message.reply('❌ This is an admin-only command.');
                return;
            }
            if (!isPremiumUser(message.author.id)) {
                message.reply('❌ This is a **Premium** feature!');
                return;
            }
            const channel = message.channel;
            if (cleanupIntervals[channel.id]) {
                message.reply('⚠️ There is already a cleanup interval running in this channel. Use !cleanupdel to stop it first.');
                return;
            }
            message.reply('🧹 Cleanup enabled! I will delete all messages in this channel now and then every hour.\n\n**Note:** You must run this command in the channel you want to clean up.');

            // helper to delete all messages in the channel (iterating in batches)
            const doFullCleanup = async (chan) => {
                let deleted = 0;
                let lastId = null;
                try {
                    while (true) {
                        const options = { limit: 100 };
                        if (lastId) options.before = lastId;
                        const messages = await chan.messages.fetch(options);
                        if (messages.size === 0) break;
                        // iterate and delete each message (including pinned)
                        for (const msg of messages.values()) {
                            try {
                                await msg.delete();
                                deleted++;
                            } catch (err) {
                                // ignore individual delete errors (rate limits / missing perms)
                            }
                        }
                        lastId = messages.last().id;
                        if (messages.size < 100) break;
                    }
                    try { await chan.send(`🧹 Cleanup complete! **${deleted}** messages deleted.`); } catch (e) { /* ignore send errors */ }
                } catch (err) {
                    try { await chan.send('❌ Error while deleting messages.'); } catch (e) { }
                }
            };

            // run immediate cleanup
            doFullCleanup(channel).then(() => {
                // after immediate run, persist nextRun
                try {
                    const stored = loadCleanupIntervals();
                    stored[channel.id] = { nextRun: Date.now() + 60 * 60 * 1000 };
                    saveCleanupIntervals(stored);
                } catch (e) { }
            });

            // schedule hourly cleanup
            const interval = setInterval(async () => {
                await doFullCleanup(channel);
                try {
                    const stored = loadCleanupIntervals();
                    stored[channel.id] = { nextRun: Date.now() + 60 * 60 * 1000 };
                    saveCleanupIntervals(stored);
                } catch (e) { }
            }, 60 * 60 * 1000);
            cleanupIntervals[channel.id] = interval;
        },

        '!cleanupdel': async (message) => {
            if (!isOwnerOrAdmin(message.member)) {
                message.reply('❌ This is an admin-only command.');
                return;
            }
            if (!isPremiumUser(message.author.id)) {
                message.reply('❌ This is a **Premium** feature!');
                return;
            }
            const channel = message.channel;
            if (!cleanupIntervals[channel.id]) {
                message.reply('❌ There is no cleanup interval running in this channel. Make sure you are in the correct channel or it was already stopped.');
                return;
            }
            clearInterval(cleanupIntervals[channel.id]);
            delete cleanupIntervals[channel.id];
            try {
                const stored = loadCleanupIntervals();
                if (stored[channel.id]) {
                    delete stored[channel.id];
                    saveCleanupIntervals(stored);
                }
            } catch (e) { }
            message.reply('🛑 Cleanup interval stopped for this channel.');
        },
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
    '!deletetwitch': async (message) => {
        const guildId = message.guild.id;
        const userId = message.author.id;
        
        const twitchLinks = loadTwitchLinks();
        
        if (!twitchLinks[guildId] || !twitchLinks[guildId][userId]) {
            message.reply('❌ Du hast keinen Twitch-Account verknüpft. Nichts zu löschen!');
            return;
        }
        
        const userData = twitchLinks[guildId][userId];
        const username = userData.twitchUsername;
        
        delete twitchLinks[guildId][userId];
        
        if (Object.keys(twitchLinks[guildId]).length === 0) {
            delete twitchLinks[guildId];
        }
        
        saveTwitchLinks(twitchLinks);
        
        message.channel.send(`✅ Deine Twitch-Daten für **${username}** wurden erfolgreich gelöscht! 🗑️`);
    },
    '!sendit': async (message) => {
        if (!message.member.permissions.has('Administrator')) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        
        const args = message.content.split(' ');
        if (args.length !== 4 || args[2].toLowerCase() !== 'to') {
            message.reply('❌ Invalid format! Use: `!sendit MESSAGE_ID to CHANNEL_ID`');
            return;
        }
        
        const messageId = args[1];
        const targetChannelId = args[3].replace(/[<#>]/g, '');
        
        try {
            const originalMessage = await message.channel.messages.fetch(messageId);
            
            if (!originalMessage) {
                message.reply('❌ Message not found in this channel!');
                return;
            }
            
            const targetChannel = message.guild.channels.cache.get(targetChannelId);
            if (!targetChannel) {
                message.reply('❌ Target channel not found!');
                return;
            }
            
            const content = originalMessage.content || '';
            const attachments = Array.from(originalMessage.attachments.values());
            
            const files = attachments.map(att => ({
                attachment: att.url,
                name: att.name
            }));
            
            if (content || files.length > 0) {
                await targetChannel.send({
                    content: content,
                    files: files
                });
                
                message.reply(`✅ Message forwarded to <#${targetChannelId}>`);
                await message.delete();
            } else {
                message.reply('❌ The message has no content or attachments to forward.');
            }
        } catch (error) {
            console.error('Sendit error:', error);
            message.reply(`❌ Failed to forward message. Error: ${error.message}`);
        }
    },
    '!testingtwitch': async (message) => {
        const BOT_OWNER_ID = '1105877268775051316';
        const isAdmin = message.member.permissions.has('Administrator');
        const isBotOwner = message.author.id === BOT_OWNER_ID;
        
        if (!isAdmin && !isBotOwner) {
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
    '!setlanguage': async (message) => {
        if (!isOwnerOrAdmin(message.member)) {
            message.reply('❌ Nur Admins oder Bot-Owner dürfen die Sprache setzen.');
            return;
        }
        const args = message.content.split(' ');
        if (args.length < 2) {
            message.reply('❌ Bitte gib eine Sprache an, z.B. `!setlanguage arabic` oder `!setlanguage english`.');
            return;
        }
        const lang = args[1].toLowerCase();
        const serverId = message.guild.id;
        const langs = loadServerLanguages();
        langs[serverId] = lang;
        saveServerLanguages(langs);
        message.reply(`✅ Serversprache wurde auf **${lang}** gesetzt. KI wird ab jetzt diese Sprache für Übersetzungen und Flirts nutzen.`);
    },
    '!translate': async (message) => {
        if (!process.env.GROQ_API_KEY) {
            message.reply('❌ Groq API Key fehlt in der .env Datei! Füge GROQ_API_KEY hinzu.');
            return;
        }
        if (!message.reference || !message.reference.messageId) {
            message.reply('❌ Bitte benutze !translate als Antwort auf eine Nachricht, die du übersetzen willst.');
            return;
        }
        try {
            const refMsg = await message.channel.messages.fetch(message.reference.messageId);
            const originalText = refMsg.content;
            if (!originalText || originalText.length < 2) {
                message.reply('❌ Die referenzierte Nachricht enthält keinen übersetzbaren Text.');
                return;
            }
            const serverId = message.guild.id;
            const langs = loadServerLanguages();
            const targetLang = langs[serverId];
            const groq = new Groq({ apiKey: process.env.GROQ_API_KEY });
            let prompt;
            if (targetLang) {
                prompt = `Du bist ein Übersetzer. Erkenne die Sprache des folgenden Textes und übersetze ihn:\n1. Ins Englische (falls Original nicht Englisch)\n2. Ins Deutsche (falls Original nicht Deutsch)\n3. In die Serversprache (${targetLang}) (falls Original nicht ${targetLang})\nAntworte im Format:\nTranslation (ENGL): ...\nTranslation (DEU): ...\nTranslation (${targetLang.toUpperCase()}): ...\n\nText: ${originalText}`;
            } else {
                prompt = `Du bist ein Übersetzer. Erkenne die Sprache des folgenden Textes und übersetze ihn:\n1. Ins Englische (falls Original nicht Englisch)\n2. Ins Deutsche (falls Original nicht Deutsch)\nAntworte im Format:\nTranslation (ENGL): ...\nTranslation (DEU): ...\n\nText: ${originalText}`;
            }
            const response = await groq.chat.completions.create({
                model: 'llama-3.3-70b-versatile',
                messages: [
                    { role: 'system', content: 'Du bist ein hilfreicher Übersetzer für Discord. Antworte immer im gewünschten Format.' },
                    { role: 'user', content: prompt }
                ],
                max_tokens: 400,
                temperature: 0.2,
                top_p: 0.95
            });
            const translation = response.choices[0].message.content.trim();
            if (translation && translation.length > 3) {
                message.reply(translation);
            } else {
                message.reply('❌ KI hat keine Übersetzung generiert. Versuch es nochmal!');
            }
        } catch (error) {
            console.error('Groq API error:', error);
            message.reply(`❌ Fehler: ${error.message || 'API Request fehlgeschlagen'}`);
        }
    },
    '!flirt': async (message) => {
        const userMessage = message.content.replace('!flirt', '').trim();
        if (!userMessage) {
            message.reply('Gib mir was zum Flirten! Beispiel: `!flirt Hey, wie geht\'s dir? 😊`');
            return;
        }
        if (!process.env.GROQ_API_KEY) {
            message.reply('❌ Groq API Key fehlt in der .env Datei! Füge GROQ_API_KEY hinzu.');
            return;
        }
        try {
            const serverId = message.guild.id;
            const langs = loadServerLanguages();
            const flirtLang = langs[serverId];
            const groq = new Groq({ apiKey: process.env.GROQ_API_KEY });
            let systemPrompt;
            if (flirtLang) {
                systemPrompt = `You are an extremely confident, sexy, and playful flirt bot. Be hot, seductive, direct and erotic - but stay charming and playful. Keep it short (1-3 sentences). Use maximum 1-2 emojis per message - no more! Be bold and provocative! CRITICAL: Antworte IMMER in ${flirtLang}. Verwende genderneutrale Begriffe falls möglich.`;
            } else {
                systemPrompt = "You are an extremely confident, sexy, and playful flirt bot. Be hot, seductive, direct and erotic - but stay charming and playful. Keep it short (1-3 sentences). Use maximum 1-2 emojis per message - no more! Be bold and provocative! CRITICAL: Detect the user's language and respond in THE EXACT SAME LANGUAGE. ALWAYS use gender-neutral terms like 'Süße/r', 'Hübsche/r', 'Schöne/r' in German or 'sweetie', 'beautiful' in English.";
            }
            const response = await groq.chat.completions.create({
                model: 'llama-3.3-70b-versatile',
                messages: [
                    { role: 'system', content: systemPrompt },
                    { role: 'user', content: userMessage }
                ],
                max_tokens: 150,
                temperature: 0.9,
                top_p: 0.95
            });
            const flirtResponse = response.choices[0].message.content.trim();
            if (flirtResponse && flirtResponse.length > 3) {
                message.reply(flirtResponse);
            } else {
                message.reply('❌ KI hat keine Antwort generiert. Versuch es nochmal! 🤔');
            }
        } catch (error) {
            console.error('Groq API error:', error);
            message.reply(`❌ Fehler: ${error.message || 'API Request fehlgeschlagen'}`);
        }
    },
    '!setbumpreminder': (message) => {
        if (!isOwnerOrAdmin(message.member)) {
            message.reply('❌ This is an admin-only command and cannot be used by regular users.');
            return;
        }
        setBumpReminder(message.channel, message.guild);
    },
    '!bumpstatus': (message) => {
        if (!isOwnerOrAdmin(message.member)) {
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
    
 
    '!setupvoice': async (message) => {
        if (!isOwnerOrAdmin(message.member)) {
            message.reply('❌ This is an admin-only command.');
            return;
        }
        
        const config = loadVoiceConfig();
        
        
        const step1 = await message.reply(
            '**Voice System Setup - Step 1/2** 🎙️\n\n' +
            'In which **Category** should the `➕ Join to Create` channel be created?\n\n' +
            '**Answer:** Send the Category ID (Right-click → Copy ID)\n' +
            '**Cancel:** Type `cancel`'
        );
        
        const filter1 = (m) => m.author.id === message.author.id;
        const collector1 = message.channel.createMessageCollector({ filter: filter1, time: 60000, max: 1 });
        
        collector1.on('collect', async (m) => {
            if (m.content.toLowerCase() === 'cancel') {
                message.reply('❌ Voice System Setup cancelled.');
                return;
            }
            
            const joinCategory = m.content.trim();
            
            
            const category1 = await message.guild.channels.fetch(joinCategory).catch(() => null);
            if (!category1 || category1.type !== 4) {
                message.reply('❌ Invalid Category ID! Please try again with `!setupvoice`.');
                return;
            }
            
            
            const step2 = await message.reply(
                '**Voice System Setup - Step 2/2** 🎙️\n\n' +
                'In which **Category** should the **created Voice Channels** be placed?\n\n' +
                '**Answer:** Send the Category ID\n' +
                '**Tip:** Can be the same or a different category'
            );
            
            const collector2 = message.channel.createMessageCollector({ filter: filter1, time: 60000, max: 1 });
            
            collector2.on('collect', async (m2) => {
                if (m2.content.toLowerCase() === 'cancel') {
                    message.reply('❌ Voice System Setup cancelled.');
                    return;
                }
                
                const voiceCategory = m2.content.trim();
                
                
                const category2 = await message.guild.channels.fetch(voiceCategory).catch(() => null);
                if (!category2 || category2.type !== 4) {
                    message.reply('❌ Invalid Category ID! Please try again with `!setupvoice`.');
                    return;
                }
                
                
                try {
                    const joinChannel = await message.guild.channels.create({
                        name: '➕ Join to Create',
                        type: 2,
                        parent: joinCategory,
                        permissionOverwrites: [
                            {
                                id: message.guild.id,
                                allow: ['Connect', 'ViewChannel']
                            }
                        ]
                    });
                    
                    config.joinToCreateChannel = joinChannel.id;
                    config.joinToCreateCategory = joinCategory;
                    config.voiceChannelCategory = voiceCategory;
                    saveVoiceConfig(config);
                    
                    const cat1 = await message.guild.channels.fetch(joinCategory);
                    const cat2 = await message.guild.channels.fetch(voiceCategory);
                    
                    message.reply(
                        `✅ **Voice System successfully set up!**\n\n` +
                        `📍 Join-to-Create: ${joinChannel} in **${cat1.name}**\n` +
                        `📍 New channels will be created in: **${cat2.name}**`
                    );
                } catch (error) {
                    console.error('Setup voice error:', error);
                    message.reply('❌ Error creating voice system.');
                }
            });
            
            collector2.on('end', (collected) => {
                if (collected.size === 0) {
                    message.reply('❌ Timeout. Please restart setup with `!setupvoice`.');
                }
            });
        });
        
        collector1.on('end', (collected) => {
            if (collected.size === 0) {
                message.reply('❌ Timeout. Please restart setup with `!setupvoice`.');
            }
        });
    },
    
    '!setupvoicelog': async (message) => {
        if (!isOwnerOrAdmin(message.member)) {
            message.reply('❌ This is an admin-only command.');
            return;
        }
        
        const config = loadVoiceConfig();
        
        try {
            
            const logChannel = await message.guild.channels.create({
                name: '📋-voice-logs',
                type: 0,
                permissionOverwrites: [
                    {
                        id: message.guild.id,
                        deny: ['ViewChannel']
                    },
                    {
                        id: message.guild.roles.everyone,
                        deny: ['ViewChannel']
                    },
                    
                    ...message.guild.roles.cache
                        .filter(role => role.permissions.has('Administrator'))
                        .map(role => ({
                            id: role.id,
                            allow: ['ViewChannel', 'SendMessages', 'ReadMessageHistory']
                        }))
                ]
            });
            
            config.voiceLogChannel = logChannel.id;
            saveVoiceConfig(config);
            
            message.reply(`✅ Voice log channel created: ${logChannel}! Only admins can see it.`);
        } catch (error) {
            console.error('Setup voice log error:', error);
            if (error.code === 50013 || error.message.includes('Missing Permissions')) {
                message.reply('❌ **Error:** Bot doesn\'t have enough permissions!\n\n**Solution:** In Server Settings → Roles → Make sure the **Bot Role is ABOVE Admin Roles**!');
            } else {
                message.reply('❌ Error creating voice log channel.');
            }
        }
    },
    
    '!voicename': async (message) => {
        const newName = message.content.replace('!voicename', '').trim();
        
        if (!newName) {
            message.reply('Usage: `!voicename New Channel Name`');
            return;
        }
        
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            await channel.setName(newName);
            message.reply(`✅ Channel renamed to **${newName}**`);
        } catch (error) {
            message.reply('❌ Error renaming channel.');
        }
    },
    
    '!voicelimit': async (message) => {
        const limit = parseInt(message.content.replace('!voicelimit', '').trim());
        
        if (isNaN(limit) || limit < 0 || limit > 99) {
            message.reply('Usage: `!voicelimit [0-99]` (0 = unlimited)');
            return;
        }
        
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            await channel.setUserLimit(limit);
            message.reply(`✅ User limit set to **${limit === 0 ? 'Unlimited' : limit}**`);
        } catch (error) {
            message.reply('❌ Error setting user limit.');
        }
    },
    
    '!voicetemplate': async (message) => {
        const template = message.content.replace('!voicetemplate', '').trim().toLowerCase();
        
        if (!message.member.voice.channelId) {
            message.reply('❌ You must be in a voice channel to use this command.');
            return;
        }
        
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        const templates = config.templates;
        if (!templates[template]) {
            message.reply(`❌ Invalid template. Available: \`gaming\`, \`study\`, \`chill\``);
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            const templateData = templates[template];
            
            await channel.setName(`${templateData.name} - ${message.author.username}`);
            if (templateData.limit > 0) {
                await channel.setUserLimit(templateData.limit);
            }
            
            channelInfo.template = template;
            saveVoiceConfig(config);
            
            message.reply(`✅ Applied **${template}** template!`);
        } catch (error) {
            message.reply('❌ Error applying template.');
        }
    },
    
    '!voicelock': async (message) => {
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            await channel.permissionOverwrites.edit(message.guild.id, {
                Connect: false
            });
            message.reply('🔒 Channel locked! Only current members can stay.');
        } catch (error) {
            message.reply('❌ Error locking channel.');
        }
    },
    
    '!voiceunlock': async (message) => {
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            await channel.permissionOverwrites.edit(message.guild.id, {
                Connect: true
            });
            message.reply('🔓 Channel unlocked!');
        } catch (error) {
            message.reply('❌ Error unlocking channel.');
        }
    },
    
    '!voicekick': async (message) => {
        const mentionedUser = message.mentions.users.first();
        
        if (!mentionedUser) {
            message.reply('Usage: `!voicekick @user`');
            return;
        }
        
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const targetMember = await message.guild.members.fetch(mentionedUser.id);
            
            if (targetMember.voice.channelId === message.member.voice.channelId) {
                await targetMember.voice.disconnect();
                message.reply(`✅ Kicked **${mentionedUser.username}** from the channel.`);
            } else {
                message.reply('❌ That user is not in your voice channel.');
            }
        } catch (error) {
            message.reply('❌ Error kicking user.');
        }
    },
    
    
    '!voicestats': async (message) => {
        if (!isPremiumUser(message.author.id)) {
            message.reply('❌ This is a **Premium** feature! Contact the bot owner for premium access.');
            return;
        }
        
        const logs = loadVoiceLogs();
        const stats = logs.stats;
        
        
        const sortedUsers = Object.entries(stats)
            .sort(([, a], [, b]) => b.totalJoins - a.totalJoins)
            .slice(0, 10);
        
        if (sortedUsers.length === 0) {
            message.reply('❌ No voice activity recorded yet.');
            return;
        }
        
        const { EmbedBuilder } = require('discord.js');
        const embed = new EmbedBuilder()
            .setColor('#11806a')
            .setTitle('🎙️ Voice Activity Stats')
            .setDescription('Top voice channel users:')
            .addFields(
                sortedUsers.map(([userId, data], index) => ({
                    name: `${index + 1}. ${data.username}`,
                    value: `Joins: **${data.totalJoins}** | Created: **${data.channelsCreated}**`,
                    inline: false
                }))
            )
            .setFooter({ text: 'Premium Feature' });
        
        message.reply({ embeds: [embed] });
    },
    
    '!voicepermit': async (message) => {
        if (!isPremiumUser(message.author.id)) {
            message.reply('❌ This is a **Premium** feature!');
            return;
        }
        
        const mentionedUser = message.mentions.users.first();
        
        if (!mentionedUser) {
            message.reply('Usage: `!voicepermit @user`');
            return;
        }
        
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            const targetMember = await message.guild.members.fetch(mentionedUser.id);
            
            await channel.permissionOverwrites.edit(targetMember.id, {
                Connect: true,
                Speak: true
            });
            
            message.reply(`✅ **${mentionedUser.username}** can now join your channel.`);
        } catch (error) {
            message.reply('❌ Error permitting user.');
        }
    },
    
    '!voicedeny': async (message) => {
        if (!isPremiumUser(message.author.id)) {
            message.reply('❌ This is a **Premium** feature!');
            return;
        }
        
        const mentionedUser = message.mentions.users.first();
        
        if (!mentionedUser) {
            message.reply('Usage: `!voicedeny @user`');
            return;
        }
        
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            const targetMember = await message.guild.members.fetch(mentionedUser.id);
            
            await channel.permissionOverwrites.edit(targetMember.id, {
                Connect: false
            });
            
            if (targetMember.voice.channelId === channel.id) {
                await targetMember.voice.disconnect();
            }
            
            message.reply(`✅ **${mentionedUser.username}** is now blocked from your channel.`);
        } catch (error) {
            message.reply('❌ Error denying user.');
        }
    },
    
    '!voiceprivate': async (message) => {
        if (!isPremiumUser(message.author.id)) {
            message.reply('❌ This is a **Premium** feature!');
            return;
        }
        
        const config = loadVoiceConfig();
        const channelInfo = config.activeChannels[message.member.voice.channelId];
        
        if (!channelInfo || channelInfo.ownerId !== message.author.id) {
            message.reply('❌ You must be in your own voice channel to use this command.');
            return;
        }
        
        try {
            const channel = await message.guild.channels.fetch(message.member.voice.channelId);
            
            await channel.permissionOverwrites.edit(message.guild.id, {
                ViewChannel: false,
                Connect: false
            });
            
            await channel.permissionOverwrites.edit(message.author.id, {
                ViewChannel: true,
                Connect: true,
                ManageChannels: true,
                MoveMembers: true
            });
            
            channelInfo.isPrivate = true;
            saveVoiceConfig(config);
            
            message.reply('🔒 Channel is now **private**! Use `!voicepermit @user` to allow specific users.');
        } catch (error) {
            message.reply('❌ Error making channel private.');
        }
    },
    
    '!cleanupvoice': async (message) => {
        if (!isOwnerOrAdmin(message.member)) {
            message.reply('❌ This is an admin-only command.');
            return;
        }
        if (!isPremiumUser(message.author.id)) {
            message.reply('❌ This is a **Premium** feature!');
            return;
        }
        const config = loadVoiceConfig();
        if (!config.voiceLogChannel) {
            message.reply('❌ No voice log channel configured. Use `!setupvoicelog` first.');
            return;
        }
        try {
            const logChannel = await message.guild.channels.fetch(config.voiceLogChannel);
            if (!logChannel) {
                message.reply('❌ Voice log channel not found.');
                return;
            }
            let deleted = 0;
            let lastId;
            while (true) {
                const options = { limit: 100 };
                if (lastId) {
                    options.before = lastId;
                }
                const messages = await logChannel.messages.fetch(options);
                if (messages.size === 0) break;
                for (const msg of messages.values()) {
                    await msg.delete();
                    deleted++;
                }
                lastId = messages.last().id;
                if (messages.size < 100) break;
            }
            message.reply(`✅ Voice log channel cleaned! Deleted **${deleted}** messages.`);
            await logChannel.send(`🧹 **Log Cleanup** - Channel cleared by ${message.author.username}`);
        } catch (error) {
            console.error('Cleanup voice error:', error);
            message.reply('❌ Error cleaning voice log channel.');
        }
    },
    
    '!deletevoice': async (message) => {
        if (!isOwnerOrAdmin(message.member)) {
            message.reply('❌ This is an admin-only command.');
            return;
        }
        if (!isPremiumUser(message.author.id)) {
            message.reply('❌ This is a **Premium** feature!');
            return;
        }
        const config = loadVoiceConfig();
        const confirmMsg = await message.reply(
            '⚠️ **WARNING: Voice System Deletion**\n\n' +
            'This will **permanently delete**:\n' +
            '• Join-to-Create channel\n' +
            '• Voice log channel\n' +
            '• All active voice channels\n' +
            '• All voice system settings\n\n' +
            '**Type `CONFIRM` to proceed or `CANCEL` to abort**'
        );
        const filter = (m) => m.author.id === message.author.id;
        const collector = message.channel.createMessageCollector({ filter, time: 30000, max: 1 });
        collector.on('collect', async (m) => {
            if (m.content.toUpperCase() === 'CANCEL') {
                message.reply('❌ Voice system deletion cancelled.');
                return;
            }
            if (m.content.toUpperCase() !== 'CONFIRM') {
                message.reply('❌ Invalid response. Deletion cancelled.');
                return;
            }
            let deletedCount = 0;
            const errors = [];
            try {
                if (config.joinToCreateChannel) {
                    try {
                        const joinChannel = await message.guild.channels.fetch(config.joinToCreateChannel);
                        if (joinChannel) {
                            await joinChannel.delete('Voice system deletion');
                            deletedCount++;
                        }
                    } catch (err) {
                        errors.push('Join-to-Create channel');
                    }
                }
                if (config.voiceLogChannel) {
                    try {
                        const logChannel = await message.guild.channels.fetch(config.voiceLogChannel);
                        if (logChannel) {
                            await logChannel.delete('Voice system deletion');
                            deletedCount++;
                        }
                    } catch (err) {
                        errors.push('Voice log channel');
                    }
                }
                if (config.activeChannels) {
                    for (const channelId of Object.keys(config.activeChannels)) {
                        try {
                            const channel = await message.guild.channels.fetch(channelId);
                            if (channel) {
                                await channel.delete('Voice system deletion');
                                deletedCount++;
                            }
                        } catch (err) {
                            errors.push(`Voice channel ${channelId}`);
                        }
                    }
                }
                config.joinToCreateChannel = null;
                config.joinToCreateCategory = null;
                config.voiceChannelCategory = null;
                config.voiceLogChannel = null;
                config.activeChannels = {};
                saveVoiceConfig(config);
                let resultMsg = `✅ **Voice System Deleted!**\n\n` +
                                `🗑️ Deleted **${deletedCount}** channels\n` +
                                `🔄 Reset all voice settings`;
                if (errors.length > 0) {
                    resultMsg += `\n\n⚠️ **Errors:** Could not delete: ${errors.join(', ')}`;
                }
                message.reply(resultMsg);
            } catch (error) {
                console.error('Delete voice system error:', error);
                message.reply('❌ Error deleting voice system. Some components may remain.');
            }
        });
        collector.on('end', (collected) => {
            if (collected.size === 0) {
                message.reply('❌ Timeout. Voice system deletion cancelled.');
            }
        });
    },
    
    '!help': (message) => {
        const embed = new EmbedBuilder()
            .setColor('#11806a')
            .setTitle('★ Bot Command Help')
            .setAuthor({ name: '!Code.Mater()', iconURL: 'https://i.imgur.com/8dF1kMw.jpeg'})
            .setDescription('Here are all available commands:')
            .addFields(
                { name: '★ General', value:
                    '`!info` - Bot info\n' +
                    '`!ping` - Testing the pingspeed of the bot\n' +
                    '`!mot` - Get motivated for the day\n' +
                    '`!gm` - Good morning messages for you and your mates\n' +
                    '`!gn` - Good night messages for you and your mates\n' +
                    '`!hi` - Say hello and get a hello from me\n' +
                    '`!coffee` - Tell your friends it\'s coffee time!\n' +
                    '`!devmeme` - Get a programming meme\n', inline: false },
                { name: '★ Security Features *Admin only, Premium*', value:
                    '`!setsecuritymod` - Enable the AI Security System for this server.\n' +
                    '  → The security system will automatically monitor all messages for spam, NSFW, invite links, and offensive language in multiple languages.\n' +
                    '  → If a violation is detected, the user will be timed out for 2 hours and warned via DM.\n' +
                    '  → You can customize the word list and settings soon.\n' +
                    '`!sban @user` - Manually ban a user\n' +
                    '`!skick @user` - Manually kick a user\n' +
                    '`!stimeout @user [minutes]` - Manually timeout a user\n' +
                    '`!stimeoutdel @user` - Remove timeout from a user', inline: false },
                { name: '★ Voice Features *Admin only, Premium*', value:
                    '`!setupvoice` - Create Join-to-Create channel *(3 channels free!)*\n' +
                    '`!setupvoicelog` - Create voice log channel *(free)*\n' +
                    '`!cleanupvoice` - Clean voice log channel\n' +
                    '`!deletevoice` - Delete entire voice system *(free)*\n' +
                    '`!voicename [name]` - Rename your voice channel\n' +
                    '`!voicelimit [0-99]` - Set user limit (0=unlimited)\n' +
                    '`!voicetemplate [gaming/study/chill]` - Apply template\n' +
                    '`!voicelock/unlock` - Lock/unlock your channel\n' +
                    '`!voicekick @user` - Kick user from your channel\n' +
                    '`!voicestats` - View voice activity stats\n' +
                    '`!voiceprivate` - Make channel private\n' +
                    '`!voicepermit @user` - Allow user to join\n' +
                    '`!voicedeny @user` - Block user from joining', inline: false },
                { name: '★ Utilities *Admin only, Premium*', value:
                    '`!sendit MESSAGE_ID to CHANNEL_ID` - Forward a message\n' +
                    '`!cleanup` - Enable hourly auto-cleanup: deletes all messages in this channel every hour. Run this command in the channel you want to clean up.\n' +
                    '`!cleanupdel` - Stop the hourly auto-cleanup for this channel. Run this command in the channel where cleanup is active.\n' +
                    '`!setupflirtlang [language]` - Set AI flirt language for this server\n' +
                    '`!removeflirtlang` - Remove AI flirt language setting for this server\n' +
                    '`!flirt [text]` - Flirt with AI-generated responses', inline: false },
                { name: '★ Twitch *Admin only*', value:
                    '`!settwitch` - Link Twitch account and configure clip notifications\n' +
                    '`!setchannel` - Create a new thread-only channel for clips \n' +
                    '`(use during !settwitch setup)`\n' +
                    '`!testingtwitch` - Test clip posting by fetching latest clip\n' +
                    '`!deletetwitch` - Delete your Twitch account data', inline: false },
                { name: '★ GitHub ❌ out of order right now!', value:
                    '`!github` - Bot owner\'s GitHub and Repos\n' +
                    '`!congithubacc` - Connect your GitHub account with the bot\n' +
                    '`!discongithubacc` - Disconnect your GitHub account\n' +
                    '`!gitrank` - Show your GitHub commit level\n' +
                    '`!gitleader` - Show the top 10 committers', inline: false },
                    { name: '★ Bump Reminders *Admin only*', value:
                     '`!setbumpreminder` - Setze einen 2-Stunden Bump-Reminder\n' +
                    '`!delbumpreminder` - Lösche den aktiven Bump-Reminder\n' +
                     '`!bumpreminder` - Aktiviere den Bump-Reminder (Alias)\n' +
                     '`!bumpreminderdel` - Deaktiviere den Bump-Reminder (Alias)\n' +
                     '`!bumpstatus` - Zeigt den Status des Bump-Reminders\n' +
                     '`!bumphelp` - Zeigt Hilfe zum Bump-System\n', inline: false },
                { name: '★ Birthday', value:
                    '`!birthdaychannel` - Set the birthday channel *Admin only*\n' +
                    '`!birthdayset` - Save your birthday', inline: false },
                { name: '★ Help Categories', value:
                    '`!mungahelpdesk` - Shows all big help categories\n' +
                    '`!helpyvoice` - Voice help\n' +
                    '`!helpysecure` - Security help\n' +
                    '`!helpytwitch` - Twitch help\n' +
                    '`!helpygithub` - GitHub help\n' +
                    '`!helpybump` - Bump/Disboard help\n' +
                    '`!helpybirth` - Birthday help', inline: false }
            )
            .setImage('https://i.imgur.com/yEnlJxN.png')
            .setFooter({ text: 'Powered by mungabee /aka ozzygirl', iconURL: 'https://avatars.githubusercontent.com/u/235295616?v=4' });
        message.reply({ embeds: [embed] });
    },
    '!ping': (message) => message.reply('Pong! Bot is running 24/7'),
    '!pingmeee': (message) => {
        message.channel.send('!pongez');
    },
    '!info': (message, BOT_INFO) => message.reply(`Bot: ${BOT_INFO.name} v${BOT_INFO.version}\nStatus: Online 24/7`),
    '!congithubacc': (message) => {
        const discordId = message.author.id;
        const loginUrl = `https://thecoffeylounge.com/github-connect.html?discordId=${discordId}`;
        message.reply(
            `To connect your GitHub account, click this link: ${loginUrl}\n` +
            'Authorize the app, then return to Discord!'
        );
    },
    '!discongithubacc': async (message) => {
        const discordUserId = message.author.id;
        let data = {};
        if (fs.existsSync('github_links.json')) {
            data = JSON.parse(fs.readFileSync('github_links.json'));
        }
        if (data[discordUserId]) {
            delete data[discordUserId];
            fs.writeFileSync('github_links.json', JSON.stringify(data, null, 2));
            try {
                const guild = message.guild;
                const member = await guild.members.fetch(discordUserId);
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
    
    
    const commandWithArgs = message.content.split(' ')[0].toLowerCase();
    const argHandler = commandHandlers[commandWithArgs];
    if (argHandler) {
        if (commandWithArgs === '!info') {
            argHandler(message, BOT_INFO);
        } else {
            argHandler(message);
        }
        return true;
    }
    
    return false;
}

module.exports = {
    handleCommand,
    commandHandlers,
    setBumpReminder,
    restoreBumpReminders,
    handleSecurityModeration,
    helpdeskOrigins,
    supportOrigins,
    loadTicketsConfig,
    saveTicketsConfig
};
