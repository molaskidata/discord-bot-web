const { PermissionFlagsBits, ChannelType } = require('discord.js');
const { loadVoiceConfig, saveVoiceConfig, addVoiceLog, isPremiumUser } = require('./voiceSystem');

// AFK Tracker
const afkTracker = new Map(); // channelId -> { userId -> lastActivity }

// Handle voice state updates
async function handleVoiceStateUpdate(oldState, newState) {
    const config = loadVoiceConfig();
    
    // User joined a channel
    if (!oldState.channelId && newState.channelId) {
        await handleUserJoined(newState, config);
    }
    
    // User left a channel
    if (oldState.channelId && !newState.channelId) {
        await handleUserLeft(oldState, config);
    }
    
    // User switched channels
    if (oldState.channelId && newState.channelId && oldState.channelId !== newState.channelId) {
        await handleUserLeft(oldState, config);
        await handleUserJoined(newState, config);
    }
    
    // User unmuted/undeafened (activity)
    if (newState.channelId && (oldState.selfMute !== newState.selfMute || oldState.selfDeaf !== newState.selfDeaf)) {
        updateAfkTracker(newState.channelId, newState.id);
    }
}

async function handleUserJoined(state, config) {
    const { channelId, member, guild } = state;
    
    // Check if user joined the "Join to Create" channel
    if (channelId === config.joinToCreateChannel) {
        await createVoiceChannel(member, guild, config);
        return;
    }
    
    // Update AFK tracker
    updateAfkTracker(channelId, member.id);
    
    // Log join
    const channel = await guild.channels.fetch(channelId);
    addVoiceLog(member.id, member.user.username, 'joined', channel.name);
    
    // Send to log channel
    await sendToLogChannel(guild, config, `✅ **${member.user.username}** joined **${channel.name}**`);
}

async function handleUserLeft(state, config) {
    const { channelId, member, guild } = state;
    
    // Check if this was a created channel
    if (config.activeChannels[channelId]) {
        const channel = await guild.channels.fetch(channelId).catch(() => null);
        
        if (channel && channel.members.size === 0) {
            // Channel is empty, delete it
            await channel.delete('Voice channel empty');
            delete config.activeChannels[channelId];
            saveVoiceConfig(config);
            
            await sendToLogChannel(guild, config, `🗑️ **${channel.name}** was deleted (empty)`);
        }
    }
    
    // Remove from AFK tracker
    if (afkTracker.has(channelId)) {
        afkTracker.get(channelId).delete(member.id);
    }
    
    // Log leave
    const channel = await guild.channels.fetch(channelId).catch(() => null);
    if (channel) {
        addVoiceLog(member.id, member.user.username, 'left', channel.name);
        await sendToLogChannel(guild, config, `❌ **${member.user.username}** left **${channel.name}**`);
    }
}

async function createVoiceChannel(member, guild, config) {
    try {
        const template = config.templates.custom;
        let channelName = `${template.name} - ${member.user.username}`;
        
        // Use configured category or fall back to Join-to-Create category
        const categoryId = config.voiceChannelCategory || config.joinToCreateCategory;
        
        const newChannel = await guild.channels.create({
            name: channelName,
            type: ChannelType.GuildVoice,
            parent: categoryId,
            userLimit: template.limit,
            permissionOverwrites: [
                {
                    id: member.id,
                    allow: [PermissionFlagsBits.ManageChannels, PermissionFlagsBits.MoveMembers]
                }
            ]
        });
        
        // Move user to new channel
        await member.voice.setChannel(newChannel);
        
        // Save channel info
        config.activeChannels[newChannel.id] = {
            ownerId: member.id,
            createdAt: Date.now(),
            template: 'custom'
        };
        saveVoiceConfig(config);
        
        // Initialize AFK tracker for this channel
        updateAfkTracker(newChannel.id, member.id);
        
        // Log creation
        addVoiceLog(member.id, member.user.username, 'created', newChannel.name);
        await sendToLogChannel(guild, config, `🎤 **${member.user.username}** created **${newChannel.name}**`);
        
    } catch (error) {
        console.error('Error creating voice channel:', error);
    }
}

function updateAfkTracker(channelId, userId) {
    if (!afkTracker.has(channelId)) {
        afkTracker.set(channelId, new Map());
    }
    afkTracker.get(channelId).set(userId, Date.now());
}

async function sendToLogChannel(guild, config, message) {
    if (!config.voiceLogChannel) return;
    
    try {
        const logChannel = await guild.channels.fetch(config.voiceLogChannel);
        if (logChannel) {
            await logChannel.send(message);
        }
    } catch (error) {
        console.error('Error sending to log channel:', error);
    }
}

// AFK Check (runs every minute)
async function checkAfkUsers(client) {
    const config = loadVoiceConfig();
    const AFK_TIMEOUT = 10 * 60 * 1000; // 10 minutes
    
    for (const [channelId, users] of afkTracker) {
        try {
            const guild = client.guilds.cache.first();
            const channel = await guild.channels.fetch(channelId).catch(() => null);
            
            if (!channel) {
                afkTracker.delete(channelId);
                continue;
            }
            
            for (const [userId, lastActivity] of users) {
                const member = channel.members.get(userId);
                
                if (!member) {
                    users.delete(userId);
                    continue;
                }
                
                // Check if user is muted/deafened for 10+ minutes
                if (member.voice.selfMute || member.voice.selfDeaf) {
                    const timeSinceActivity = Date.now() - lastActivity;
                    
                    if (timeSinceActivity >= AFK_TIMEOUT) {
                        await member.voice.disconnect('AFK for 10+ minutes');
                        users.delete(userId);
                        await sendToLogChannel(guild, config, `⏰ **${member.user.username}** was kicked from **${channel.name}** (AFK)`);
                    }
                }
            }
        } catch (error) {
            console.error('Error checking AFK:', error);
        }
    }
}

// Auto cleanup voice logs (runs every 5 hours)
async function autoCleanupVoiceLogs(client) {
    const config = loadVoiceConfig();
    
    if (!config.voiceLogChannel) return;
    
    try {
        const guild = client.guilds.cache.first();
        const logChannel = await guild.channels.fetch(config.voiceLogChannel).catch(() => null);
        
        if (!logChannel) return;
        
        // Delete all messages
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
        
        console.log(`🧹 Auto-cleanup: Deleted ${deleted} messages from voice log channel`);
        await logChannel.send(`🧹 **Auto-Cleanup** - Voice logs cleared (${deleted} messages deleted)`);
        
    } catch (error) {
        console.error('Error in auto cleanup voice logs:', error);
    }
}

module.exports = {
    handleVoiceStateUpdate,
    checkAfkUsers,
    autoCleanupVoiceLogs
};
