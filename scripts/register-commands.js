const { REST, Routes } = require('discord.js');
require('dotenv').config();

const CLIENT_ID = process.env.CLIENT_ID || process.env.DISCORD_CLIENT_ID || '';
const GUILD_ID = process.argv[2] || process.env.TEST_GUILD_ID || '1410329844272595050';
const token = process.env.DISCORD_TOKEN;

if (!CLIENT_ID || !token) {
  console.error('Missing CLIENT_ID or DISCORD_TOKEN in environment.');
  process.exit(1);
}

const commands = [
  {
    name: 'help',
    description: 'Shows help for the bot (alias for !help)'
  },
  {
    name: 'settwitch',
    description: 'Link or configure Twitch settings for this server',
    options: [
      {
        name: 'channel',
        description: 'Channel to post Twitch clips in',
        type: 7, // CHANNEL
        required: false
      }
    ]
  }
];

(async () => {
  try {
    const rest = new REST({ version: '10' }).setToken(token);
    console.log(`Registering ${commands.length} commands to guild ${GUILD_ID}`);
    await rest.put(Routes.applicationGuildCommands(CLIENT_ID, GUILD_ID), { body: commands });
    console.log('Commands registered successfully.');
  } catch (err) {
    console.error('Failed to register commands:', err);
    process.exit(1);
  }
})();
