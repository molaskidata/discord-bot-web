import './style.css'

document.querySelector('#app').innerHTML = `
  <div>
    <h1>☕ Coffee & Codes Activity</h1>
    <p>Discord activity for the Coffee & Codes community</p>
    <button id="connect-btn">Connect with Discord</button>
    <div id="activity-content" style="display: none;">
      <h2>🎮 Activity is running!</h2>
      <p>Welcome to the Coffee & Codes activity!</p>
      <p>Hang out together and code! ☕💻</p>
    </div>
  </div>
`;

document.querySelector('#connect-btn').addEventListener('click', () => {
  document.querySelector('#connect-btn').style.display = 'none';
  document.querySelector('#activity-content').style.display = 'block';
});
