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

// Security system state per guild
const securitySystemEnabled = {};

// --- Security Moderation Handler ---
async function handleSecurityModeration(message) {
    if (!message.guild) return;
    const guildId = message.guild.id;
    if (!securitySystemEnabled[guildId]) return;
    if (!isOwnerOrAdmin(message.member)) return; // Don't moderate admins/owners

    const content = message.content.toLowerCase();
    // Check for invite links
    const inviteRegex = /(discord\.gg\/|discordapp\.com\/invite\/|discord\.com\/invite\/)/i;
    if (inviteRegex.test(content)) {
        await timeoutAndWarn(message, 'Invite links are not allowed!');
        return;
    }
    // Check for spam (simple: repeated characters/words, can be improved)
    if (/([a-zA-Z0-9])\1{6,}/.test(content) || /(.)\s*\1{6,}/.test(content)) {
        await timeoutAndWarn(message, 'Spam detected!');
        return;
    }
    // Check for blacklisted words
    for (const word of securityWordLists) {
        if (content.includes(word)) {
            await timeoutAndWarn(message, `Inappropriate language detected: "${word}"`);
            return;
        }
    }
    // Check for NSFW images (basic: attachment filename, can be improved with AI)
    if (message.attachments && message.attachments.size > 0) {
        for (const [, attachment] of message.attachments) {
            const name = attachment.name.toLowerCase();
            if (name.match(/(nude|nudes|porn|dick|boobs|sex|fuck|pussy|tits|vagina|penis|clit|anal|ass|nsfw|xxx|18\+|dickpic|dickpic|nacktbilder|nackt|milf|slut|cum|cumshot|hure|huren|arsch|fick|ficki|ficks|titten|titt|t1tt|nud3|nud3s|p0rn|p0rno|p3nis|kitzler|scheide|schlampe|nutt|nippel|nacktbilder|nackt|nude|nudes|nutt|p0rn|p0rno|p3nis|penis|porn|porno|puss|pussy|s3x|scheide|schlampe|sex|sexual|slut|slutti|t1tt|titt|titten|vag1na|vagina)/)) {
                await timeoutAndWarn(message, 'NSFW/explicit image detected!');
                return;
            }
        }
    }
}

// --- Timeout and Warn Helper ---
async function timeoutAndWarn(message, reason) {
    try {
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
            if (securitySystemEnabled[guildId]) {
                message.reply('⚠️ Security system is already enabled for this server.');
                return;
            }
            securitySystemEnabled[guildId] = true;
            message.reply('🛡️ Security system has been enabled for this server! The bot will now monitor for spam, NSFW, invite links, and offensive language in all supported languages.');
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
        // ...existing code...
    '!ahoy': (message) => message.reply(getRandomResponse(pirateGreetings)),
    '!farewell': (message) => message.reply(getRandomResponse(pirateFarewell)),
    '!treasure': (message) => message.reply(getRandomResponse(treasureQuotes)),
    '!sea': (message) => message.reply(getRandomResponse(seaQuotes)),
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
            .setImage('https://images.steamusercontent.com/ugc/18345562890096640134/3633D8BC964A13D4B21DA399A640643AFB7B8B70/?imw=1920&&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=false')
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
            .setTitle('⚓ Pirate Bot Commands')
            .setDescription('Arrr! Here be the commands ye can use:')
            .addFields(
                { name: '» Greetings', value: 
                    '`!ahoy` - Pirate greeting\n' +
                    '`!farewell` - Say goodbye pirate-style\n', inline: false },
                { name: '» Treasure & Sea', value:
                    '`!treasure` - Treasure quotes\n' +
                    '`!sea` - Sea of Thieves quotes\n' +
                    '`!piratecode` - The Pirate Code\n', inline: false },
                { name: '» Fun Commands', value:
                    '`!crew` - Show crew count\n' +
                    '`!dice` - Roll the dice\n' +
                    '`!compass` - Check direction\n', inline: false }
            )
            .setFooter({ text: 'Yo ho ho!' });
        message.reply({ embeds: [embed] });
    },
    '!helpme': (message) => {
        const { EmbedBuilder } = require('discord.js');
        const embed = new EmbedBuilder()
            .setColor('#2C1810')
            .setTitle('⚓ PirateBot - Command Overview')
            .setDescription('**Ahoy, matey!** Welcome aboard the finest pirate bot on the seven seas!\n\nHere be all the commands to help ye navigate:')
            .addFields(
                { name: '» Social Commands', value:
                    '`!ahoy` - Get a hearty pirate greeting\n' +
                    '`!farewell` - Bid farewell like a true buccaneer\n', inline: true },
                { name: '» Loot & Adventure', value:
                    '`!treasure` - Wisdom about treasure\n' +
                    '`!sea` - Sea of Thieves quotes\n' +
                    '`!piratecode` - Read the sacred Pirate Code\n', inline: true },
                { name: '» Fun & Games', value:
                    '`!dice` - Roll yer lucky dice\n' +
                    '`!compass` - Check the wind direction\n' +
                    '`!crew` - See how many mates be aboard\n', inline: false },
                { name: '» Information', value:
                    '`!helpme` - Show this help menu\n' +
                    '`!piratehelp` - Quick command reference\n', inline: false }
            )
            .setImage('https://images.steamusercontent.com/ugc/56962680127490223/8405C14A667B05E602D82BB7E566BFC22AD075D3/?imw=1920&&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=false')
            .setFooter({ 
                text: 'Fair winds and following seas! | Made by mungabee',
                iconURL: 'https://avatars.githubusercontent.com/u/235295616?v=4'
            });
        message.reply({ embeds: [embed] });
    }
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
