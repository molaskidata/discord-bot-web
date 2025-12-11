const pirateGreetings = [
    "Ahoy, Matey! ⚓",
    "Arrr, what be ye needin'?",
    "Shiver me timbers! Ahoy there!",
    "Yo ho ho! What brings ye to these waters?"
];

const pirateFarewell = [
    "Fair winds and following seas, matey! ⚓",
    "May yer compass always point true!",
    "Safe travels, ye scallywag!",
    "Until we meet again on the high seas!"
];

const treasureQuotes = [
    "X marks the spot!",
    "Treasure be waitin' for the brave!",
    "Gold doubloons for all!",
    "The greatest treasure be the crew ye sail with!"
];

const seaQuotes = [
    "The sea be callin', matey!",
    "A pirate's life for me! ⚓",
    "Dead men tell no tales!",
    "Hoist the colors!"
];

function getRandomResponse(array) {
    return array[Math.floor(Math.random() * array.length)];
}

const commandHandlers = {
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
            })
            .setTimestamp();
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
            .setColor('#8B4513')
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
            .setColor('#CD7F32')
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
            })
            .setTimestamp();
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
    commandHandlers
};
