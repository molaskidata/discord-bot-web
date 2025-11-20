// utils.js
// Utility functions for the bot

function getRandomResponse(responseArray) {
    return responseArray[Math.floor(Math.random() * responseArray.length)];
}

module.exports = {
    getRandomResponse
};
