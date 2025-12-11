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
        message.reply(
            '**† The Pirate Code †**\n\n' +
            '1. Every pirate has a vote in affairs of moment\n' +
            '2. Every pirate has equal title to fresh provisions\n' +
            '3. No striking one another aboard ship\n' +
            '4. Keep your piece, cutlass, and pistols clean\n' +
            '5. The captain shall have two shares of a prize\n' +
            '6. Take what ye can, give nothin\' back!'
        );
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
