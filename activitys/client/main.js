import './style.css'

document.querySelector('#app').innerHTML = `
  <div>
    <h1>☕ Coffee & Codes Activity</h1>
    <p>Discord Activity für die Coffee & Codes Community</p>
    <button id="connect-btn">Mit Discord verbinden</button>
    <div id="activity-content" style="display: none;">
      <h2>🎮 Activity läuft!</h2>
      <p>Willkommen in der Coffee & Codes Activity!</p>
      <p>Hier könnt ihr zusammen abhängen und coden! ☕💻</p>
    </div>
  </div>
`;

document.querySelector('#connect-btn').addEventListener('click', () => {
  document.querySelector('#connect-btn').style.display = 'none';
  document.querySelector('#activity-content').style.display = 'block';
});
