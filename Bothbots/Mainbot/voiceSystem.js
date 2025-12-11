const fs = require('fs');
const path = require('path');

// Files
const VOICE_CONFIG_FILE = path.join(__dirname, 'voice_config.json');
const VOICE_LOG_FILE = path.join(__dirname, 'voice_logs.json');

// Premium Users (hardcoded for now)
const PREMIUM_USERS = [
    '235295616', // ozzygirl/mungabee - replace with actual Discord ID
    // Add more user IDs here
];

// Load/Save Functions
function loadVoiceConfig() {
    if (fs.existsSync(VOICE_CONFIG_FILE)) {
        return JSON.parse(fs.readFileSync(VOICE_CONFIG_FILE));
    }
    return {
        joinToCreateChannel: null,
        activeChannels: {},
        voiceLogChannel: null,
        templates: {
            gaming: { name: '🎮 Gaming Room', limit: 0 },
            study: { name: '📚 Study Session', limit: 4 },
            chill: { name: '💤 Chill Zone', limit: 0 },
            custom: { name: '🔊 Voice Chat', limit: 0 }
        }
    };
}

function saveVoiceConfig(data) {
    fs.writeFileSync(VOICE_CONFIG_FILE, JSON.stringify(data, null, 2));
}

function loadVoiceLogs() {
    if (fs.existsSync(VOICE_LOG_FILE)) {
        return JSON.parse(fs.readFileSync(VOICE_LOG_FILE));
    }
    return { logs: [], stats: {} };
}

function saveVoiceLogs(data) {
    fs.writeFileSync(VOICE_LOG_FILE, JSON.stringify(data, null, 2));
}

// Check if user has premium
function isPremiumUser(userId) {
    return PREMIUM_USERS.includes(userId);
}

// Add voice log entry
function addVoiceLog(userId, username, action, channelName) {
    const logs = loadVoiceLogs();
    
    logs.logs.push({
        userId,
        username,
        action,
        channelName,
        timestamp: new Date().toISOString()
    });
    
    // Update stats
    if (!logs.stats[userId]) {
        logs.stats[userId] = {
            username,
            totalJoins: 0,
            totalTime: 0,
            channelsCreated: 0
        };
    }
    
    if (action === 'joined') logs.stats[userId].totalJoins++;
    if (action === 'created') logs.stats[userId].channelsCreated++;
    
    // Keep only last 1000 logs
    if (logs.logs.length > 1000) {
        logs.logs = logs.logs.slice(-1000);
    }
    
    saveVoiceLogs(logs);
    return logs;
}

module.exports = {
    loadVoiceConfig,
    saveVoiceConfig,
    loadVoiceLogs,
    saveVoiceLogs,
    isPremiumUser,
    addVoiceLog
};
