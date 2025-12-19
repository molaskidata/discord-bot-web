// Original archived copy of PiratBot/commands.js
// (Saved by archive operation)
// --- Security System Word Lists (multi-language, extend as needed) ---
const securityWordLists = [
    // German (provided)
    'anal', 'anus', 'arsch', 'boobs', 'cl1t', 'clit', 'dick', 'dickpic', 'fick', 'ficki', 'ficks', 'fuck', 'fucking', 'hure', 'huren', 'hurens', 'kitzler', 'milf', 'nackt', 'nacktbilder', 'nippel', 'nud3', 'nude', 'nudes', 'nutt', 'p0rn', 'p0rno', 'p3nis', 'penis', 'porn', 'porno', 'puss', 'pussy', 's3x', 'scheide', 'schlampe', 'sex', 'sexual', 'slut', 'slutti', 't1tt', 'titt', 'titten', 'vag1na', 'vagina',
    'arschloch', 'asozial', 'bastard', 'behindert', 'depp', 'dödel', 'dumm', 'dummi', 'hund', 'hundesohn', 'idiot', 'lappen', 'lappi', 'opfa', 'opfer', 'sohnedings', 'sohnemann', 'sohns', 'spast', 'spasti', 'wichser', 'wix', 'wixx', 'wixxer',
    'geh sterben', 'gehsterben', 'go die', 'ich bring dich um', 'ich töte dich', 'kill yourself', 'killyourself', 'kys', 'self harm', 'selfharm', 'sterb', 'suizid', 'töd dich', 'töt dich', 'verreck', 'verreckt', 'cl1ck', 'click here', 'discordgift', 'free nitro', 'freenitro', 'gift you nitro', 'steamgift', 'abschlacht', 'abschlachten', 'abst3chen', 'abstechen', 'abstich', 'angreifen', 'att4ck', 'attack', 'attackieren', 'aufhaengen', 'aufhängen', 'ausloeschen', 'auslöschen', 'ausradieren', 'bedroh', 'bedrohe', 'bedrohen', 'blut', 'brechdirdieknochen', 'bring dich um', 'bringdichum', 'bringmichum', 'erdrücken', 'erdruecken', 'erhaengen', 'erhängen', 'ermorden', 'erschies', 'erschießen', 'erstech', 'erstechen', 'erwuergen', 'erwürg', 'erwürgen', 'gefährd', 'gefährlich', 'k1ll', 'kill', 'kille', 'killer', 'knochenbrechen', 'm0rd', 'm4ssaker', 'massaker', 'mord', 'morden', 'pruegeln', 'prügeln', 'schiess', 'schieß', 'schlagdich', 'schlagmich', 'shoot', 'stech', 'stich', 'toeten', 'töten', 'umbr1ng', 'umbracht', 'umbringen',
    // English (partial, extend as needed)
    'anal', 'anus', 'ass', 'boobs', 'clit', 'dick', 'dickpic', 'fuck', 'fucking', 'whore', 'milf', 'nude', 'nudes', 'nipple', 'porn', 'porno', 'pussy', 'sex', 'slut', 'tits', 'vagina', 'bastard', 'idiot', 'dumb', 'stupid', 'retard', 'spastic', 'wanker', 'go die', 'kill yourself', 'kys', 'suicide', 'self harm', 'selfharm', 'die', 'murder', 'kill', 'attack', 'blood', 'shoot', 'stab', 'hang', 'dangerous', 'massacre', 'threat', 'gift nitro', 'free nitro', 'discordgift', 'click here', 'steamgift',
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
async function sendSecurityLog(message, reason, matched = "") {
    try {
        const guildId = message.guild ? message.guild.id : null;
        if (!guildId) return;
        const cfg = securityConfig[guildId];
        const channelId = cfg && cfg.logChannelId ? cfg.logChannelId : null;
        if (!channelId) return;
        const client = message.client;
        const ch = await client.channels.fetch(channelId).catch(() => null);
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
        if (message.attachments && message.attachments.size > 0) {
            for (const [, att] of message.attachments) {
                files.push(att.url);
            }
        }
        await ch.send({ content: `Security event in ${message.guild.name} (${message.guild.id})`, embeds: [embed], files: files });
        try {
            const logEntry = {
                time: new Date().toISOString(),
                guildId: message.guild.id,
                guildName: message.guild.name,
                userId: message.author.id,
                userTag: message.author.tag,
                action: reason,
                matched: matched || (message.content || ''),
                content: message.content || '',
                attachments: files
            };
            const path = 'security_logs_pirate.jsonl';
            // rotate weekly
            try {
                if (fs.existsSync(path)) {
                    const stats = fs.statSync(path);
                    const age = Date.now() - stats.mtimeMs;
                    const week = 7 * 24 * 60 * 60 * 1000;
                    if (age > week) {
                        const ts = new Date(stats.mtimeMs).toISOString().slice(0, 10);
                        fs.renameSync(path, `${path}.${ts}`);
                    }
                }
            } catch (e) { }
            fs.appendFileSync(path, JSON.stringify(logEntry) + '\n');
        } catch (e) { /* ignore file write errors */ }
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
    // (archived: full command collection saved in this file)
};

// (rest of original file archived)
