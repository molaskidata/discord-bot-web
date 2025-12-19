// Original archived copy of PiratBot/piratbot.js
// (Saved by archive operation)
const { handleSecurityModeration } = require('./commands');
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

// ticket meta in memory
global.ticketMeta = new Map();

client.on('interactionCreate', async (interaction) => {
    if (!interaction.isStringSelectMenu()) return;
    if (interaction.customId !== 'support_select') return;
    try {
        const choice = interaction.values[0];
        const selectionMap = {
            support_technical: 'Technical Issue',
            support_spam: 'Spam / Scam',
            support_abuse: 'Abuse / Harassment',
            support_ad: 'Advertising / Recruitment',
            support_bug: 'Bug / Feature Request',
            support_other: 'Other'
        };
        const selectionLabel = selectionMap[choice] || choice;
        const guild = interaction.guild;
        const user = interaction.user;
        const chanName = `ticket-${user.username.toLowerCase().replace(/[^a-z0-9]/g, '')}-${Date.now() % 10000}`;
        const overwrites = [{ id: guild.id, deny: ['ViewChannel'] }, { id: user.id, allow: ['ViewChannel', 'SendMessages', 'ReadMessageHistory'] }];
        guild.roles.cache.filter(r => r.permissions.has('Administrator')).forEach(role => {
            overwrites.push({ id: role.id, allow: ['ViewChannel', 'SendMessages', 'ReadMessageHistory'] });
        });
        overwrites.push({ id: client.user.id, allow: ['ViewChannel', 'SendMessages', 'ReadMessageHistory', 'ManageChannels'] });
        const ticketChannel = await guild.channels.create({ name: chanName, type: 0, permissionOverwrites: overwrites });
        const { EmbedBuilder, ActionRowBuilder, ButtonBuilder, ButtonStyle } = require('discord.js');
        const embed = new EmbedBuilder().setColor('#2f3136').setTitle(`Ticket: ${selectionLabel}`).setDescription(`Opened by <@${user.id}> (${user.id})\n\nPls continue and write your issue. We will be here for you as quick as we can!`).setFooter({ text: `Category: ${selectionLabel}` });
        const row = new ActionRowBuilder().addComponents(
            new ButtonBuilder().setCustomId('ticket_close').setLabel('Close Ticket').setStyle(ButtonStyle.Danger),
            new ButtonBuilder().setCustomId('ticket_log').setLabel('Log Ticket').setStyle(ButtonStyle.Secondary),
            new ButtonBuilder().setCustomId('ticket_save').setLabel('Save Transcript').setStyle(ButtonStyle.Primary)
        );
        await ticketChannel.send({ content: `<@${user.id}>`, embeds: [embed], components: [row] });
        await interaction.reply({ content: `✅ Ticket created: ${ticketChannel}`, ephemeral: true });
        global.ticketMeta.set(ticketChannel.id, { userId: user.id, category: selectionLabel, guildId: guild.id });
    } catch (err) {
        console.error('PirateBot support_select handler error:', err);
        try { await interaction.reply({ content: '❌ Failed to create ticket channel.', ephemeral: true }); } catch (e) { }
    }
});

client.on('interactionCreate', async (interaction) => {
    if (!interaction.isButton()) return;
    const id = interaction.customId;
    const channel = interaction.channel;
    const guild = interaction.guild;
    try {
        const meta = global.ticketMeta ? global.ticketMeta.get(channel.id) : null;
        if (!meta) { await interaction.reply({ content: 'This button is only available inside a ticket channel.', ephemeral: true }); return; }
        const fs = require('fs');
        // build transcript
        let messages = [];
        let lastId;
        while (true) {
            const options = { limit: 100 };
            if (lastId) options.before = lastId;
            const batch = await channel.messages.fetch(options);
            if (!batch) break;
            messages = messages.concat(Array.from(batch.values()).map(m => ({ author: m.author.tag, id: m.author.id, content: m.content, time: m.createdAt.toISOString() })));
            if (batch.size < 100) break;
            lastId = batch.last().id;
        }
        messages = messages.reverse();
        const transcript = messages.map(m => `[${m.time}] ${m.author} (${m.id}): ${m.content}`).join('\n');

        // load pirate tickets config
        const { loadTicketsConfig } = require('./commands');
        const cfg = loadTicketsConfig();
        const logChannelId = cfg[guild.id]?.logChannelId;

        if (id === 'ticket_close') { await interaction.reply({ content: 'Closing ticket...', ephemeral: true }); setTimeout(() => { channel.delete().catch(() => { }); if (global.ticketMeta) global.ticketMeta.delete(channel.id); }, 1000); return; }
        if (!logChannelId) { await interaction.reply({ content: 'No log channel configured. Ask an admin to run !munga-ticketsystem.', ephemeral: true }); return; }
        const filename = `pirate_ticket_${guild.id}_${channel.id}_${Date.now()}.txt`;
        fs.writeFileSync(filename, transcript);
        const logChan = guild.channels.cache.get(logChannelId);
        if (logChan) { await logChan.send({ content: `Ticket transcript from ${channel.name} (created by <@${meta.userId}>):`, files: [filename] }); }
        try { fs.unlinkSync(filename); } catch (e) { }
        await interaction.reply({ content: '✅ Ticket transcript saved to log channel.', ephemeral: true });
    } catch (err) { console.error('PirateBot ticket button handler error:', err); try { await interaction.reply({ content: 'An error occurred while handling the ticket action.', ephemeral: true }); } catch (e) { } }
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
    handleSecurityModeration(message);
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
