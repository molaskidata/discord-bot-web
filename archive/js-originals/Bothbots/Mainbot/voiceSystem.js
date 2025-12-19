const fs = require('fs');
const path = require('path');

const VOICE_CONFIG_FILE = path.join(__dirname, 'voice_config.json');
const VOICE_LOG_FILE = path.join(__dirname, 'voice_logs.json');

const PREMIUM_USERS = [
    '1105877268775051316',
];


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

function isPremiumUser(userId) {
    return PREMIUM_USERS.includes(userId);
}

function addVoiceLog(userId, username, action, channelName) {
    const logs = loadVoiceLogs();

    logs.logs.push({
        userId,
        username,
        action,
        channelName,
        timestamp: new Date().toISOString()
    });


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