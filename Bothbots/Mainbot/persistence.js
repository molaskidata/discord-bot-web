const fs = require('fs');
const path = require('path');

function resolvePath(fileName) {
    return path.resolve(process.cwd(), fileName);
}

function loadJSON(fileName) {
    const p = resolvePath(fileName);
    try {
        if (!fs.existsSync(p)) return {};
        const raw = fs.readFileSync(p, 'utf8');
        return JSON.parse(raw || '{}');
    } catch (e) {
        return {};
    }
}

function saveJSON(fileName, obj) {
    const p = resolvePath(fileName);
    try {
        fs.writeFileSync(p, JSON.stringify(obj, null, 2), 'utf8');
        return true;
    } catch (e) {
        return false;
    }
}

async function backupFiles(fileNames) {
    for (const f of fileNames) {
        try {
            const data = loadJSON(f);
            saveJSON(f, data);
        } catch (e) {
            // ignore individual failures
        }
    }
}

function schedulePeriodicBackup(fileNames, intervalMs = 5 * 60 * 1000) {
    setInterval(() => { backupFiles(fileNames); }, intervalMs);
}

module.exports = { loadJSON, saveJSON, backupFiles, schedulePeriodicBackup };
