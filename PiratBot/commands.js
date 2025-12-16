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

const fs = require('fs');

// Security system config per guild (persisted)
const SECURITY_FILE = 'pirate_security_config.json';
let securityConfig = {};
function loadSecurityConfig() {
    if (fs.existsSync(SECURITY_FILE)) {
        try { return JSON.parse(fs.readFileSync(SECURITY_FILE)); } catch (e) { return {}; }
    }
    return {};
}
function saveSecurityConfig() {
    fs.writeFileSync(SECURITY_FILE, JSON.stringify(securityConfig, null, 2));
}
securityConfig = loadSecurityConfig();

// helper to know if enabled
function isSecurityEnabled(guildId) {
    return securityConfig[guildId] && securityConfig[guildId].enabled;
}

// Security system state per guild (kept for backward compatibility)
const securitySystemEnabled = {};

// --- Security Moderation Handler ---
async function handleSecurityModeration(message) {
    if (!message.guild) return;
    const guildId = message.guild.id;
    if (!isSecurityEnabled(guildId) && !securitySystemEnabled[guildId]) return;
    if (isOwnerOrAdmin(message.member)) return; // Don't moderate admins/owners

    const content = (message.content || '').toLowerCase();
    // helper to send a log before we delete/timeout
    async function report(reason, matched) {
        await sendSecurityLog(message, reason, matched);
        await timeoutAndWarn(message, reason);
    }

    // Check for invite links
    const inviteRegex = /(discord\.gg\/|discordapp\.com\/invite\/|discord\.com\/invite\/)/i;
    if (inviteRegex.test(content)) {
        await report('Invite links are not allowed!', 'invite link');
        return;
    }
    // Check for spam (simple: repeated characters/words, can be improved)
    if (/([a-zA-Z0-9])\1{6,}/.test(content) || /(.)\s*\1{6,}/.test(content)) {
        await report('Spam detected!', 'spam');
        return;
    }
    // Check for blacklisted words
    for (const word of securityWordLists) {
        if (content.includes(word)) {
            await report(`Inappropriate language detected: "${word}"`, word);
            return;
        }
    }
    // Check for NSFW images (basic: attachment filename, can be improved with AI)
    if (message.attachments && message.attachments.size > 0) {
        for (const [, attachment] of message.attachments) {
            const name = (attachment.name || '').toLowerCase();
            if (name.match(/(nude|nudes|porn|dick|boobs|sex|fuck|pussy|tits|vagina|penis|clit|anal|ass|nsfw|xxx|18\+|dickpic|nacktbilder|nackt|milf|slut|cum|cumshot|hure|huren|arsch|fick|titten|t1tt|nud3|p0rn|p0rno|p3nis|kitzler|scheide|schlampe|nutt|nippel)/)) {
                await report('NSFW/explicit image detected!', 'attachment: ' + name);
                return;
            }
        }
    }
}

// --- Timeout and Warn Helper ---
async function timeoutAndWarn(message, reason) {
    try {
        // log the event to the configured warn log channel (if any)
        try {
            await sendSecurityLog(message, reason);
        } catch (e) {
            // ignore logging errors
        }
        await message.delete();
        await message.member.timeout(2 * 60 * 60 * 1000, reason); // 2h timeout
        await message.author.send(`You have been timed out for 2 hours for: ${reason}`);
    } catch (err) {
        // Ignore errors (e.g. cannot DM user)
    }
}

function isOwnerOrAdmin(member) {
    return member.permissions.has('Administrator');
}
module.exports.handleSecurityModeration = handleSecurityModeration;
// --- Security logging helper ---
async function sendSecurityLog(message, reason, matched="") {
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
            for (const [,att] of message.attachments) {
                files.push(att.url);
            }
        }
        await ch.send({ content: `Security event in ${message.guild.name} (${message.guild.id})`, embeds: [embed], files: files });
    } catch (e) {
        // ignore
    }
}
const { getRandomResponse } = require('../Bothbots/Mainbot/utils');
const { loadVoiceConfig, saveVoiceConfig, isPremiumUser, loadVoiceLogs } = require('../Bothbots/Mainbot/voiceSystem');
const pirateGreetings = [
    "Ahoy, Matey! ⚓",
    "Arrr, what be ye needin'?",
    "Shiver me timbers! Ahoy there!",
    "Yo ho ho! What brings ye to these waters?"
];

const pirateFarewell = [
    "Fair winds and following seas, matey! ⚓",
    "May your sails stay full and your rum never run dry!",
    "Until our ships cross paths again, matey!",
    "Yo ho ho, farewell!"
];

const commandHandlers = {
        '!setsecuritymod': async (message) => {
            if (!isOwnerOrAdmin(message.member)) {
                message.reply('❌ This is an admin-only command.');
                return;
            }
            const guildId = message.guild.id;
            if (isSecurityEnabled(guildId) || securitySystemEnabled[guildId]) {
                message.reply('⚠️ Security system is already enabled for this server.');
                return;
            }
            // enable in config and ask for log channel
            securityConfig[guildId] = securityConfig[guildId] || {};
            securityConfig[guildId].enabled = true;
            saveSecurityConfig();

            const step = await message.reply('🛡️ Security system has been enabled for this server! The bot will now monitor for spam, NSFW, invite links, and offensive language in all supported languages.\n\nPlease provide the CHANNEL ID where I should send the warn logs (type `none` to disable logging, or type `!setchannelsec` to let me create a warn-log channel for you).');

            const filter = (m) => m.author.id === message.author.id;
            const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
            collector.on('collect', async (m) => {
                const val = (m.content || '').trim();
                if (val.toLowerCase() === 'none') {
                    securityConfig[guildId].logChannelId = null;
                    saveSecurityConfig();
                    message.reply('✅ Security enabled with no logging. All actions will still be taken but not logged. All right! Now lean back, I work now for you and yes. 24/7 baby ;))');
                    return;
                }
                if (val === '!setchannelsec' || val.toLowerCase() === 'create') {
                    try {
                        const ch = await message.guild.channels.create({ name: 'warn-logs', type: 0, permissionOverwrites: [{ id: message.guild.id, deny: ['ViewChannel'] }] });
                        securityConfig[guildId].logChannelId = ch.id;
                        saveSecurityConfig();
                        message.reply(`✅ Created and set warn log channel: ${ch}. All right! Now lean back, I work now for you and yes. 24/7 baby ;))`);
                    } catch (e) {
                        message.reply('❌ Failed to create log channel. Please provide a channel ID or create one and run the command again.');
                    }
                    return;
                }
                // try to accept channel id
                const maybeId = val.replace(/[^0-9]/g, '');
                if (!maybeId) { message.reply('❌ Invalid input. Provide a channel ID, `none`, or `!setchannelsec`.'); return; }
                const ch = await message.guild.channels.fetch(maybeId).catch(()=>null);
                if (!ch) { message.reply('❌ Channel not found. Make sure I can access it and provide the numeric Channel ID.'); return; }
                securityConfig[guildId].logChannelId = ch.id;
                saveSecurityConfig();
                message.reply(`✅ Warn log channel set to ${ch}. All right! Now lean back, I work now for you and yes. 24/7 baby ;))`);
            });
            collector.on('end', (collected) => {
                if (collected.size === 0) {
                    message.reply('⌛ Timeout: no channel provided. You can run `!setsecuritymod` again to set logging.');
                }
            });
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
                if (arg === 'on' || arg === 'enable') {
                    securityConfig[gid].enabled = true; saveSecurityConfig();
                    message.reply('✅ Security enabled for this server.'); return;
                }
                if (arg === 'off' || arg === 'disable') {
                    securityConfig[gid].enabled = false; saveSecurityConfig();
                    message.reply('✅ Security disabled for this server.'); return;
                }
                message.reply('Usage: !security <on|off|status>');
            },
            '!setseclog': async (message) => {
                if (!isOwnerOrAdmin(message.member)) { message.reply('❌ Admins only'); return; }
                const parts = message.content.split(' ').filter(Boolean);
                const arg = parts[1] ? parts[1].trim() : null;
                const gid = message.guild.id;
                securityConfig[gid] = securityConfig[gid] || {};
                if (!arg) { message.reply('Usage: !setseclog <channelId|none|create>'); return; }
                if (arg.toLowerCase() === 'none') { securityConfig[gid].logChannelId = null; saveSecurityConfig(); message.reply('✅ Logging disabled for this server.'); return; }
                if (arg.toLowerCase() === 'create') {
                    try { const ch = await message.guild.channels.create({ name: 'warn-logs', type: 0, permissionOverwrites: [{ id: message.guild.id, deny: ['ViewChannel'] }] }); securityConfig[gid].logChannelId = ch.id; saveSecurityConfig(); message.reply(`✅ Created warn-log channel: ${ch}`); } catch (e) { message.reply('❌ Failed to create channel'); }
                    return;
                }
                const maybe = arg.replace(/[^0-9]/g,'');
                if (!maybe) { message.reply('❌ Invalid channel id'); return; }
                const ch = await message.guild.channels.fetch(maybe).catch(()=>null);
                if (!ch) { message.reply('❌ Channel not found'); return; }
                securityConfig[gid].logChannelId = ch.id; saveSecurityConfig(); message.reply(`✅ Log channel set to ${ch}`);
            },
        '!sban': async (message) => {
            if (!isOwnerOrAdmin(message.member)) {
                message.reply('❌ This is an admin-only command.');
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
            if (!isOwnerOrAdmin(message.member)) {
                message.reply('❌ This is an admin-only command.');
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
            if (!isOwnerOrAdmin(message.member)) {
                message.reply('❌ This is an admin-only command.');
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
            if (!isOwnerOrAdmin(message.member)) {
                message.reply('❌ This is an admin-only command.');
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
                        console.error('Setup voice error (PiratBot):', error);
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
                        }
                    ]
                });

                config.voiceLogChannel = logChannel.id;
                saveVoiceConfig(config);

                message.reply(`✅ Voice log channel created: ${logChannel}! Only admins can see it.`);
            } catch (error) {
                console.error('Setup voice log error (PiratBot):', error);
                message.reply('❌ Error creating voice log channel.');
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
                if (templateData.limit > 0) await channel.setUserLimit(templateData.limit);
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
                await channel.permissionOverwrites.edit(message.guild.id, { Connect: false });
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
                await channel.permissionOverwrites.edit(message.guild.id, { Connect: true });
                message.reply('🔓 Channel unlocked!');
            } catch (error) {
                message.reply('❌ Error unlocking channel.');
            }
        },

        '!voicekick': async (message) => {
            const mentionedUser = message.mentions.users.first();
            if (!mentionedUser) { message.reply('Usage: `!voicekick @user`'); return; }
            const config = loadVoiceConfig();
            const channelInfo = config.activeChannels[message.member.voice.channelId];
            if (!channelInfo || channelInfo.ownerId !== message.author.id) { message.reply('❌ You must be in your own voice channel to use this command.'); return; }
            try {
                const targetMember = await message.guild.members.fetch(mentionedUser.id);
                if (targetMember.voice.channelId === message.member.voice.channelId) {
                    await targetMember.voice.disconnect();
                    message.reply(`✅ Kicked **${mentionedUser.username}** from the channel.`);
                } else {
                    message.reply('❌ That user is not in your voice channel.');
                }
            } catch (error) { message.reply('❌ Error kicking user.'); }
        },

        '!voicestats': async (message) => {
            if (!isPremiumUser(message.author.id)) { message.reply('❌ This is a **Premium** feature! Contact the bot owner for premium access.'); return; }
            const logs = loadVoiceLogs();
            const stats = logs.stats;
            const sortedUsers = Object.entries(stats).sort(([, a], [, b]) => b.totalJoins - a.totalJoins).slice(0, 10);
            if (sortedUsers.length === 0) { message.reply('❌ No voice activity recorded yet.'); return; }
            const { EmbedBuilder } = require('discord.js');
            const embed = new EmbedBuilder()
                .setColor('#11806a')
                .setTitle('🎙️ Voice Activity Stats')
                .setDescription('Top voice channel users:')
                .addFields(sortedUsers.map(([userId, data], index) => ({ name: `${index + 1}. ${data.username}`, value: `Joins: **${data.totalJoins}** | Created: **${data.channelsCreated}**`, inline: false })))
                .setFooter({ text: 'Premium Feature' });
            message.reply({ embeds: [embed] });
        },

        '!voicepermit': async (message) => {
            if (!isPremiumUser(message.author.id)) { message.reply('❌ This is a **Premium** feature!'); return; }
            const mentionedUser = message.mentions.users.first(); if (!mentionedUser) { message.reply('Usage: `!voicepermit @user`'); return; }
            const config = loadVoiceConfig(); const channelInfo = config.activeChannels[message.member.voice.channelId];
            if (!channelInfo || channelInfo.ownerId !== message.author.id) { message.reply('❌ You must be in your own voice channel to use this command.'); return; }
            try {
                const channel = await message.guild.channels.fetch(message.member.voice.channelId);
                const targetMember = await message.guild.members.fetch(mentionedUser.id);
                await channel.permissionOverwrites.edit(targetMember.id, { Connect: true, Speak: true });
                message.reply(`✅ **${mentionedUser.username}** can now join your channel.`);
            } catch (error) { message.reply('❌ Error permitting user.'); }
        },

        '!voicedeny': async (message) => {
            if (!isPremiumUser(message.author.id)) { message.reply('❌ This is a **Premium** feature!'); return; }
            const mentionedUser = message.mentions.users.first(); if (!mentionedUser) { message.reply('Usage: `!voicedeny @user`'); return; }
            const config = loadVoiceConfig(); const channelInfo = config.activeChannels[message.member.voice.channelId];
            if (!channelInfo || channelInfo.ownerId !== message.author.id) { message.reply('❌ You must be in your own voice channel to use this command.'); return; }
            try {
                const channel = await message.guild.channels.fetch(message.member.voice.channelId);
                const targetMember = await message.guild.members.fetch(mentionedUser.id);
                await channel.permissionOverwrites.edit(targetMember.id, { Connect: false });
                if (targetMember.voice.channelId === channel.id) await targetMember.voice.disconnect();
                message.reply(`✅ **${mentionedUser.username}** is now blocked from your channel.`);
            } catch (error) { message.reply('❌ Error denying user.'); }
        },

        '!voiceprivate': async (message) => {
            if (!isPremiumUser(message.author.id)) { message.reply('❌ This is a **Premium** feature!'); return; }
            const config = loadVoiceConfig(); const channelInfo = config.activeChannels[message.member.voice.channelId];
            if (!channelInfo || channelInfo.ownerId !== message.author.id) { message.reply('❌ You must be in your own voice channel to use this command.'); return; }
            try {
                const channel = await message.guild.channels.fetch(message.member.voice.channelId);
                await channel.permissionOverwrites.edit(message.guild.id, { ViewChannel: false, Connect: false });
                await channel.permissionOverwrites.edit(message.author.id, { ViewChannel: true, Connect: true, ManageChannels: true, MoveMembers: true });
                channelInfo.isPrivate = true; saveVoiceConfig(config);
                message.reply('🔒 Channel is now **private**! Use `!voicepermit @user` to allow specific users.');
            } catch (error) { message.reply('❌ Error making channel private.'); }
        },

        '!cleanupvoice': async (message) => {
            if (!isOwnerOrAdmin(message.member)) { message.reply('❌ This is an admin-only command.'); return; }
            if (!isPremiumUser(message.author.id)) { message.reply('❌ This is a **Premium** feature!'); return; }
            const config = loadVoiceConfig(); if (!config.voiceLogChannel) { message.reply('❌ No voice log channel configured. Use `!setupvoicelog` first.'); return; }
            try {
                const logChannel = await message.guild.channels.fetch(config.voiceLogChannel);
                if (!logChannel) { message.reply('❌ Voice log channel not found.'); return; }
                let deleted = 0; let lastId;
                while (true) {
                    const options = { limit: 100 };
                    if (lastId) options.before = lastId;
                    const messages = await logChannel.messages.fetch(options);
                    if (messages.size === 0) break;
                    for (const msg of messages.values()) { await msg.delete(); deleted++; }
                    lastId = messages.last().id; if (messages.size < 100) break;
                }
                message.reply(`✅ Voice log channel cleaned! Deleted **${deleted}** messages.`);
                await logChannel.send(`🧹 **Log Cleanup** - Channel cleared by ${message.author.username}`);
            } catch (error) { console.error('Cleanup voice error (PiratBot):', error); message.reply('❌ Error cleaning voice log channel.'); }
        },

        '!deletevoice': async (message) => {
            if (!isOwnerOrAdmin(message.member)) { message.reply('❌ This is an admin-only command.'); return; }
            if (!isPremiumUser(message.author.id)) { message.reply('❌ This is a **Premium** feature!'); return; }
            const config = loadVoiceConfig();
            const confirmMsg = await message.reply('⚠️ **WARNING: Voice System Deletion**\n\nType `CONFIRM` to proceed or `CANCEL` to abort');
            const filter = (m) => m.author.id === message.author.id;
            const collector = message.channel.createMessageCollector({ filter, time: 30000, max: 1 });
            collector.on('collect', async (m) => {
                if (m.content.toUpperCase() === 'CANCEL') { message.reply('❌ Voice system deletion cancelled.'); return; }
                if (m.content.toUpperCase() !== 'CONFIRM') { message.reply('❌ Invalid response. Deletion cancelled.'); return; }
                let deletedCount = 0; const errors = [];
                try {
                    if (config.joinToCreateChannel) { try { const joinChannel = await message.guild.channels.fetch(config.joinToCreateChannel); if (joinChannel) { await joinChannel.delete('Voice system deletion'); deletedCount++; } } catch (err) { errors.push('Join-to-Create channel'); } }
                    if (config.voiceLogChannel) { try { const logChannel = await message.guild.channels.fetch(config.voiceLogChannel); if (logChannel) { await logChannel.delete('Voice system deletion'); deletedCount++; } } catch (err) { errors.push('Voice log channel'); } }
                    if (config.activeChannels) { for (const channelId of Object.keys(config.activeChannels)) { try { const channel = await message.guild.channels.fetch(channelId); if (channel) { await channel.delete('Voice system deletion'); deletedCount++; } } catch (err) { errors.push(`Voice channel ${channelId}`); } } }
                    config.joinToCreateChannel = null; config.joinToCreateCategory = null; config.voiceChannelCategory = null; config.voiceLogChannel = null; config.activeChannels = {}; saveVoiceConfig(config);
                    let resultMsg = `✅ **Voice System Deleted!**\n\n🗑️ Deleted **${deletedCount}** channels\n🔄 Reset all voice settings`;
                    if (errors.length > 0) resultMsg += `\n\n⚠️ **Errors:** Could not delete: ${errors.join(', ')}`;
                    message.reply(resultMsg);
                } catch (error) { console.error('Delete voice system error (PiratBot):', error); message.reply('❌ Error deleting voice system. Some components may remain.'); }
            });
            collector.on('end', (collected) => { if (collected.size === 0) message.reply('❌ Timeout. Voice system deletion cancelled.'); });
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
                const files = attachments.map(att => ({ attachment: att.url, name: att.name }));

                if (content || files.length > 0) {
                    await targetChannel.send({ content, files });
                    message.reply(`✅ Message forwarded to <#${targetChannelId}>`);
                    await message.delete();
                } else {
                    message.reply('❌ The message has no content or attachments to forward.');
                }
            } catch (error) {
                console.error('PiratBot sendit error:', error);
                message.reply(`❌ Failed to forward message. Error: ${error.message}`);
            }
        },
        // ...existing code...
    '!ahoy': (message) => message.reply(getRandomResponse(pirateGreetings)),
    '!farewell': (message) => message.reply(getRandomResponse(pirateFarewell)),
    // removed: '!treasure' and '!sea' (deprecated by request)
    '!piratecode': (message) => {
        const { EmbedBuilder } = require('discord.js');
        const embed = new EmbedBuilder()
            .setColor('#2C1810')
            .setTitle('⚓ The Pirate Code ⚓')
            .setDescription('**The Code of the Brethren, set down by the pirates Morgan and Bartholomew**\n\n*"The Code is more what you\'d call guidelines than actual rules." - Captain Barbossa*')
            .addFields(
                { name: 'Article I', value: 'Every man shall have an equal vote in affairs of moment. He shall have an equal title to the fresh provisions or strong liquors at any time seized, and shall use them at pleasure unless a scarcity may make it necessary for the common good that a retrenchment may be voted.', inline: false },
                { name: 'Article II', value: 'Every man shall be called fairly in turn by the list on board of prizes, because over and above their proper share, they are allowed a shift of clothes. But if they defraud the company to the value of even one dollar in plate, jewels or money, they shall be marooned.', inline: false },
                { name: 'Article III', value: 'None shall game for money either with dice or cards.', inline: false },
                { name: 'Article IV', value: 'The lights and candles should be put out at eight at night, and if any of the crew desire to drink after that hour they shall sit upon the open deck without lights.', inline: false },
                { name: 'Article V', value: 'Each man shall keep his piece, cutlass and pistols at all times clean and ready for action.', inline: false },
                { name: 'Article VI', value: 'No boy or woman to be allowed amongst them. If any man shall be found seducing any of the latter sex and carrying her to sea in disguise, he shall suffer death.', inline: false },
                { name: 'Article VII', value: 'He that shall desert the ship or his quarters in time of battle shall be punished by death or marooning.', inline: false },
                { name: 'Article VIII', value: 'None shall strike another on board the ship, but every man\'s quarrel shall be ended on shore by sword or pistol in this manner.', inline: false },
                { name: 'Article IX', value: 'No man shall talk of breaking up their way of living till each has a share of 1,000. Every man who shall become a cripple or lose a limb in the service shall have 800 pieces of eight from the common stock.', inline: false },
                { name: 'Article X', value: 'The captain and the quartermaster shall each receive two shares of a prize, the master gunner and boatswain, one and one half shares, all other officers one and one quarter, and private gentlemen of fortune one share each.', inline: false },
                { name: 'Article XI', value: 'The musicians shall have rest on the Sabbath Day only by right. On all other days by favour only.', inline: false }
            )
            .setImage('https://i.imgur.com/lqJBNWW.png')
            .setFooter({ 
                text: 'Fair winds and following seas! | Made by mungabee',
                iconURL: 'https://avatars.githubusercontent.com/u/235295616?v=4'
            });
        message.reply({ embeds: [embed] });
    },
    '!crew': (message) => {
        const members = message.guild.memberCount;
        message.reply(`Arrr! This ship has **${members}** crew members aboard! ⚓`);
    },
    '!dice': (message) => {
        const roll = Math.floor(Math.random() * 6) + 1;
        message.reply(`» Ye rolled a **${roll}**! ${roll === 6 ? 'Lucky dog!' : ''}`);
    },
    '!compass': (message) => {
        const directions = ['North ↑', 'South ↓', 'East →', 'West ←', 'Northeast ↗', 'Northwest ↖', 'Southeast ↘', 'Southwest ↙'];
        const direction = directions[Math.floor(Math.random() * directions.length)];
        message.reply(`⚓ The compass points **${direction}**!`);
    },
    '!piratehelp': (message) => {
        const { EmbedBuilder } = require('discord.js');
        const embed = new EmbedBuilder()
            .setColor('#2C1810')
            .setTitle('⚓ Pirate Bot — Quick Reference')
            .setDescription('Arrr! Quick list of available commands (short reference):')
            .addFields(
                { name: '» Greetings', value:
                    '`!ahoy` — Pirate greeting\n' +
                    '`!farewell` — Say goodbye pirate-style\n', inline: true },
                { name: '» Fun & Games', value:
                    '`!crew` — Show crew count\n' +
                    '`!dice` — Roll the dice\n' +
                    '`!compass` — Check direction\n' +
                    '`!games` — Games menu (Battleship & Mine/Raid)\n' +
                    '`!bs start @user|<id>` — Start Battleship (mention or ID)\n' +
                    '`!bs attack A1` — Attack coordinate\n' +
                    '`!mine` / `!gold` / `!raid @user|<id>` — Mine/inspect/raid', inline: false },
                { name: '» Security (admins)', value:
                    '`!setsecuritymod` — Enable security + set warn-log channel\n' +
                    '`!sban @user` — Ban a user\n' +
                    '`!skick @user` — Kick a user\n' +
                    '`!stimeout @user [minutes]` — Timeout a user\n' +
                    '`!stimeoutdel @user` — Remove timeout', inline: false },
                { name: '» Voice System', value:
                    '`!setupvoice` — Initialize Join-to-Create system (admin)\n' +
                    '`!setupvoicelog` — Create voice-log channel (admin)\n' +
                    '`!voicename <name>` — Rename your private voice channel\n' +
                    '`!voicelimit <n>` — Set user limit\n' +
                    '`!voicelock` / `!voiceunlock` — Lock/unlock your channel', inline: false },
                { name: '» Tickets & Admin', value:
                    '`!munga-supportticket` — Post support-ticket menu\n' +
                    '`!munga-ticketsystem` — Configure ticket logging (admin)\n' +
                    '`!sendit` — Forward a message to another channel (admin)', inline: false },
                { name: '» Misc', value:
                    '`!helpme` — Full help (detailed)\n' +
                    '`!piratehelp` — This short reference', inline: false }
            )
            .setImage('https://i.imgur.com/p95YIAZ.png')
            .setFooter({ text: 'Yo ho ho! — short reference' });
        message.reply({ embeds: [embed] });
    },
    '!helpme': (message) => {
        const { EmbedBuilder } = require('discord.js');
        const embed = new EmbedBuilder()
            .setColor('#2C1810')
            .setTitle('⚓ PirateBot - Full Command List')
            .setDescription('**Ahoy, matey!** This is the full command reference for PirateBot. Use `!piratehelp` for a short list.')
            .addFields(
                { name: '» Greetings', value:
                    '`!ahoy` — Pirate greeting\n' +
                    '`!farewell` — Pirate farewell\n' , inline: true },
                { name: '» Core / Info', value:
                    '`!helpme` — Full help (this message)\n' +
                    '`!piratehelp` — Short reference\n' +
                    '`!piratecode` — Read the Pirate Code\n', inline: true },
                { name: '» Fun & Games', value:
                    '`!crew` — Show crew count\n' +
                    '`!dice` — Roll the dice\n' +
                    '`!compass` — Check direction\n' +
                    '`!games` — Open games menu (Battleship & Mine/Raid)\n' +
                    '`!bs start @user | <userId>` — Start Battleship (mention or ID)\n' +
                    '`!bs attack A1` — Attack a coordinate\n' +
                    '`!mine` — Mine gold\n' +
                    '`!gold` — Show your gold balance\n' +
                    '`!raid @user | <userId>` — Attempt to raid another player', inline: false },
                { name: '» Security & Moderation (admins)', value:
                    '`!setsecuritymod` — Enable security & set warn-log (interactive)\n' +
                    '`!sban @user` — Ban user\n' +
                    '`!skick @user` — Kick user\n' +
                    '`!stimeout @user [minutes]` — Timeout user\n' +
                    '`!stimeoutdel @user` — Remove timeout\n', inline: false },
                { name: '» Voice System', value:
                    '`!setupvoice` — Setup join-to-create channel (admin)\n' +
                    '`!setupvoicelog` — Create voice log channel (admin)\n' +
                    '`!voicename <name>` — Rename your private voice channel\n' +
                    '`!voicelimit <n>` — Set user limit for your channel\n' +
                    '`!voicelock` / `!voiceunlock` — Lock/unlock your channel\n' , inline: false },
                { name: '» Tickets & Admin', value:
                    '`!munga-supportticket` — Post support ticket selection menu\n' +
                    '`!munga-ticketsystem` — Configure ticket logging (admin)\n' +
                    '`!sendit` — Forward a message to another channel (admin)\n', inline: false },
                { name: '» Misc / Troubleshooting', value:
                    '`!ping` — Check bot latency (if present)\n' +
                    'If a command fails, run it with the correct syntax or mention the user. For Battleship you can use a user mention or a user ID.', inline: false }
            )
            .setImage('https://i.imgur.com/RHZtWpV.png')
            .setFooter({ 
                text: 'Fair winds and following seas! | Made by mungabee',
                iconURL: 'https://avatars.githubusercontent.com/u/235295616?v=4'
            });
        message.reply({ embeds: [embed] });
    }
};

// --- Simple Games & Persistence ---
const GAMES_FILE = 'pirate_games.json';
function loadGameData() {
    const fs = require('fs');
    if (fs.existsSync(GAMES_FILE)) return JSON.parse(fs.readFileSync(GAMES_FILE));
    return { players: {}, battles: {} };
}
function saveGameData(data) {
    const fs = require('fs');
    fs.writeFileSync(GAMES_FILE, JSON.stringify(data, null, 2));
}

function coordToIndex(coord) {
    // expect A1..E5
    const m = /^([A-Ea-e])(\d)$/.exec(coord.trim());
    if (!m) return null;
    const row = m[1].toUpperCase().charCodeAt(0) - 65;
    const col = parseInt(m[2], 10) - 1;
    if (row < 0 || row > 4 || col < 0 || col > 4) return null;
    return { r: row, c: col };
}

function makeEmptyBoard() {
    return Array.from({ length: 5 }, () => Array(5).fill(0));
}

function placeRandomShips(board, sizes = [2,2,2]) {
    // place ships value 1 on board, naive random placement
    for (const size of sizes) {
        let placed = false;
        for (let attempt=0; attempt<200 && !placed; attempt++) {
            const horiz = Math.random() < 0.5;
            const r = Math.floor(Math.random()*5);
            const c = Math.floor(Math.random()*5);
            const cells = [];
            for (let i=0;i<size;i++) {
                const rr = r + (horiz?0:i);
                const cc = c + (horiz?i:0);
                if (rr>4||cc>4) { cells.length=0; break; }
                cells.push([rr,cc]);
            }
            if (cells.length===0) continue;
            // check overlap
            if (cells.some(([rr,cc])=>board[rr][cc]===1)) continue;
            cells.forEach(([rr,cc])=> board[rr][cc]=1);
            placed = true;
        }
    }
}

function boardToDisplay(board, reveal=false) {
    // 0 empty, 1 ship, 2 miss, 3 hit
    const rows = ['A','B','C','D','E'];
    let out = '  1 2 3 4 5\n';
    for (let r=0;r<5;r++){
        out += rows[r] + ' ';
        for (let c=0;c<5;c++){
            const v = board[r][c];
            let ch = '·';
            if (v===2) ch = 'o';
            if (v===3) ch = 'X';
            if (reveal && v===1) ch = 'S';
            out += ch + ' ';
        }
        out += '\n';
    }
    return "```\n" + out + "```";
}

// Gaming embed command
commandHandlers['!games'] = (message) => {
    const { EmbedBuilder } = require('discord.js');
    const embed = new EmbedBuilder()
        .setTitle('Pirate Games')
        .setDescription('Play interactive pirate games: Battleship and Gold Raid. Use the commands below to play!')
        .setImage('https://i.imgur.com/o8M34zS.png')
        .addFields(
            { name: 'Battleship (PvP)', value: '!bs start @user - Start a battleship game. Then use !bs attack A1 to shoot. (5x5 grid, 3 ships)' },
            { name: 'Mine & Raid', value: '!mine - Mine gold for your ship. !gold - Show your gold. !raid @user - Attempt to steal gold from another player (cooldown).' }
        )
        .setFooter({ text: 'Have fun sailing the seas!' });
    message.reply({ embeds: [embed] });
};

// Battleship: start and attack
commandHandlers['!bs'] = async (message) => {
    const parts = message.content.split(' ').filter(Boolean);
    const sub = parts[1] ? parts[1].toLowerCase() : null;
    const data = loadGameData();
    if (sub === 'start') {
        let target = message.mentions.users.first();
        if (!target && parts[2]) {
            const maybeId = parts[2].replace(/[^0-9]/g, '');
            if (maybeId) {
                const member = await message.guild.members.fetch(maybeId).catch(()=>null);
                if (member) target = member.user;
            }
        }
        if (!target) { message.reply('Usage: !bs start @user  OR  !bs start <userId>'); return; }
        if (target.id === message.author.id) { message.reply('Cannot play against yourself.'); return; }
        const gid = `${message.guild.id}_${message.channel.id}_${Date.now()%10000}`;
        const boardA = makeEmptyBoard();
        const boardB = makeEmptyBoard();
        placeRandomShips(boardA); placeRandomShips(boardB);
        data.battles[gid] = { playerA: message.author.id, playerB: target.id, boardA, boardB, turn: message.author.id, finished: false };
        saveGameData(data);
        message.channel.send('⚓ Battleship started between <@' + message.author.id + '> and <@' + target.id + '>! Game ID: `' + gid + '`. <@' + message.author.id + '> starts. Use `!bs attack A1` to fire.');
        return;
    }
    if (sub === 'attack') {
        const coord = parts[2];
        if (!coord) { message.reply('Usage: !bs attack A1'); return; }
        // find active game where author is player and not finished
        const gid = Object.keys(data.battles).find(k => {
            const g = data.battles[k];
            return !g.finished && (g.playerA===message.author.id || g.playerB===message.author.id);
        });
        if (!gid) { message.reply('No active battles found for you. Start one with `!bs start @user`.'); return; }
        const game = data.battles[gid];
        if (game.turn !== message.author.id) { message.reply('Not your turn.'); return; }
        const idx = coordToIndex(coord);
        if (!idx) { message.reply('Invalid coordinate. Use A1..E5.'); return; }
        const opponentBoard = (game.playerA===message.author.id) ? game.boardB : game.boardA;
        const cell = opponentBoard[idx.r][idx.c];
        if (cell === 2 || cell === 3) { message.reply('Already attacked that coordinate.'); return; }
        let reply = '';
        if (cell === 1) {
            opponentBoard[idx.r][idx.c] = 3; // hit
            reply = `💥 Hit at ${coord}!`;
        } else {
            opponentBoard[idx.r][idx.c] = 2; // miss
            reply = `🌊 Miss at ${coord}.`;
        }
        // check win
        const opponentHasShips = opponentBoard.some(row => row.some(v => v===1));
        if (!opponentHasShips) {
            game.finished = true;
            reply += `\n🏴‍☠️ <@${message.author.id}> sank all ships and won!`;
            // award gold
            const pdata = data.players[message.author.id] || { gold: 0, lastMine: 0 };
            pdata.gold = (pdata.gold || 0) + 50;
            data.players[message.author.id] = pdata;
            reply += ' You received 50 gold.';
        } else {
            // switch turn
            game.turn = (game.playerA === message.author.id) ? game.playerB : game.playerA;
            reply += `\nNext: <@${game.turn}>`;
        }
        data.battles[gid] = game;
        saveGameData(data);
        // show small board of opponent (no reveal)
        message.channel.send(reply);
        return;
    }
    message.reply('Battleship commands: `!bs start @user` or `!bs attack A1`.');
};

// Mining and raid mini-game
commandHandlers['!mine'] = (message) => {
    const data = loadGameData();
    const pid = message.author.id;
    const now = Date.now();
    const p = data.players[pid] || { gold: 0, lastMine: 0 };
    if (now - (p.lastMine || 0) < 60*1000) { message.reply('You must wait 60s between mines.'); return; }
    const found = Math.floor(Math.random()*20) + 5; // 5-24 gold
    p.gold = (p.gold||0) + found;
    p.lastMine = now;
    data.players[pid] = p;
    saveGameData(data);
    message.reply(`⛏️ You mined ${found} gold! Current gold: ${p.gold}`);
};

commandHandlers['!gold'] = (message) => {
    const data = loadGameData();
    const p = data.players[message.author.id] || { gold: 0 };
    message.reply(`🏴‍☠️ You have **${p.gold||0}** gold stored on your ship.`);
};

commandHandlers['!raid'] = (message) => {
    const target = message.mentions.users.first();
    if (!target) { message.reply('Usage: !raid @user'); return; }
    if (target.id === message.author.id) { message.reply('You cannot raid yourself.'); return; }
    const data = loadGameData();
    const attacker = data.players[message.author.id] || { gold: 0, lastRaid: 0 };
    const defender = data.players[target.id] || { gold: 0 };
    const now = Date.now();
    if (now - (attacker.lastRaid || 0) < 2*60*1000) { message.reply('Raid cooldown: 2 minutes.'); return; }
    if (!defender.gold || defender.gold <= 0) { message.reply('Target has no gold to steal.'); return; }
    // chance-based steal
    const success = Math.random() < 0.5; // 50%
    let stolen = 0;
    if (success) {
        stolen = Math.max(1, Math.floor(defender.gold * (Math.random()*0.15 + 0.05))); // steal 5-20%
        defender.gold = Math.max(0, defender.gold - stolen);
        attacker.gold = (attacker.gold||0) + stolen;
        attacker.lastRaid = now;
        data.players[message.author.id] = attacker;
        data.players[target.id] = defender;
        saveGameData(data);
        message.reply(`🏴‍☠️ Raid successful! You stole **${stolen}** gold from <@${target.id}>.`);
    } else {
        attacker.lastRaid = now;
        data.players[message.author.id] = attacker;
        saveGameData(data);
        message.reply(`💥 Raid failed! <@${target.id}> defended their ship.`);
    }
};

// Support origins map and tickets config for PiratBot
const supportOrigins = new Map();
const TICKETS_CONFIG_FILE = 'pirate_tickets_config.json';
function loadTicketsConfig() {
    const fs = require('fs');
    if (fs.existsSync(TICKETS_CONFIG_FILE)) return JSON.parse(fs.readFileSync(TICKETS_CONFIG_FILE));
    return {};
}
function saveTicketsConfig(data) {
    const fs = require('fs');
    fs.writeFileSync(TICKETS_CONFIG_FILE, JSON.stringify(data, null, 2));
}

// add support ticket posting command (image different)
commandHandlers['!munga-supportticket'] = async (message) => {
    message.reply('Please send the target Channel ID where the support ticket embed should be posted. (60 seconds)');
    const filter = m => m.author.id === message.author.id && /^\d{17,20}$/.test(m.content.trim());
    const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
    collector.on('collect', async (msg) => {
        const channelId = msg.content.trim();
        const targetChannel = message.guild.channels.cache.get(channelId);
        if (!targetChannel) { message.reply('❌ Channel not found.'); return; }
        const { ActionRowBuilder, StringSelectMenuBuilder, EmbedBuilder } = require('discord.js');
        const supportEmbed = new EmbedBuilder()
            .setColor('#2f3136')
            .setAuthor({ name: 'Support Ticket', iconURL: 'https://i.imgur.com/f29ONGJ.png' })
            .setTitle('Create a Support Ticket')
            .setDescription('Need help? Choose the appropriate option from the menu below to create a support ticket. Provide as much detail as possible when prompted.\n\nExamples of what to report:\n• Technical issues (bot errors, command failures, crashes)\n• Server abuse or harassment (spam, targeted threats, moderation requests)\n• Scam or phishing attempts (suspicious links, impersonation)\n• Advertising / recruitment (unsolicited promotions or bot invites)\n• Bug reports & feature requests (steps to reproduce, expected behavior)\n• Other (billing, access, or custom requests)')
            .setImage('https://i.imgur.com/f29ONGJ.png')
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
        supportOrigins.set(sent.id, message.channel.id);
        message.reply('✅ Support ticket embed posted in the requested channel!');
    });
    collector.on('end', (collected) => { if (collected.size === 0) message.reply('❌ Time expired.'); });
};

// admin command to configure ticket log channel
commandHandlers['!munga-ticketsystem'] = async (message) => {
    if (!message.member.permissions.has('Administrator')) { message.reply('❌ Admin only command.'); return; }
    message.reply('Send the Log Channel ID where ticket transcripts should be posted, or type **!create** to let the bot create one. (60s)');
    const filter = m => m.author.id === message.author.id;
    const collector = message.channel.createMessageCollector({ filter, time: 60000, max: 1 });
    collector.on('collect', async (m) => {
        const val = m.content.trim();
        let logChannelId = null;
        if (val.toLowerCase() === '!create') {
            try {
                const created = await message.guild.channels.create({ name: 'tickets-log', type: 0, permissionOverwrites: [{ id: message.guild.id, deny: ['ViewChannel'] }] });
                message.guild.roles.cache.filter(r => r.permissions.has('Administrator')).forEach(role => { created.permissionOverwrites.create(role, { ViewChannel: true, SendMessages: true, ReadMessageHistory: true }).catch(() => {}); });
                created.permissionOverwrites.create(message.client.user.id, { ViewChannel: true, SendMessages: true, ReadMessageHistory: true }).catch(() => {});
                logChannelId = created.id;
            } catch (err) { message.reply('❌ Failed to create log channel.'); return; }
        } else {
            const match = val.match(/<#?(\d+)>?/);
            if (!match) { message.reply('❌ Invalid channel ID.'); return; }
            const chan = message.guild.channels.cache.get(match[1]);
            if (!chan) { message.reply('❌ Channel not found.'); return; }
            logChannelId = match[1];
        }
        const cfg = loadTicketsConfig();
        cfg[message.guild.id] = { logChannelId };
        saveTicketsConfig(cfg);
        message.reply(`✅ Ticket system configured. Log channel: <#${logChannelId}>`);
    });
    collector.on('end', (collected) => { if (collected.size === 0) message.reply('❌ Time expired.'); });
};

function handleCommand(message) {
    const handler = commandHandlers[message.content.toLowerCase()];
    if (handler) {
        handler(message);
        return true;
    }
    return false;
}

module.exports = {
    handleCommand,
    commandHandlers,
    handleSecurityModeration
};
module.exports.supportOrigins = supportOrigins;
module.exports.loadTicketsConfig = loadTicketsConfig;
module.exports.saveTicketsConfig = saveTicketsConfig;
