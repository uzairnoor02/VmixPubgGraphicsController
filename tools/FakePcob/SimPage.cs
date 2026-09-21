namespace FakePcob;

/// The `/_sim` control page (Task 6) - one self-contained HTML file, inline CSS/JS, no CDN, usable
/// on a phone. Polls /_sim/status once a second.
public static class SimPage
{
    public const string Html = """
<!doctype html>
<html>
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>FakePcob control</title>
<style>
  :root { color-scheme: dark; }
  body { margin: 0; padding: 16px; font-family: -apple-system, Segoe UI, Roboto, sans-serif; background: #0b0d12; color: #eef2f8; }
  h1 { font-size: 18px; margin: 0 0 12px; }
  .row { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
  button { background: #1c2230; color: #eef2f8; border: 1px solid #333c4f; border-radius: 8px; padding: 10px 14px; font-size: 15px; }
  button:active { background: #2a3346; }
  button.primary { background: #2b6cff; border-color: #2b6cff; }
  input[type=number] { width: 80px; padding: 8px; border-radius: 8px; border: 1px solid #333c4f; background: #11141b; color: #eef2f8; }
  .tick { font-size: 28px; font-weight: 700; margin: 8px 0; }
  .status { color: #9aa7bd; font-size: 13px; margin-bottom: 12px; }
  .current { background: #16321f; border: 1px solid #2c6b3f; border-radius: 8px; padding: 10px; margin-bottom: 10px; font-weight: 600; }
  ul { list-style: none; padding: 0; margin: 0; }
  li { padding: 8px 10px; border-bottom: 1px solid #1e2432; font-size: 14px; }
  li.now { background: #1a2540; border-radius: 6px; }
</style>
</head>
<body>
  <h1>FakePcob control</h1>
  <div class="tick" id="tick">T000</div>
  <div class="status" id="status">loading...</div>
  <div class="row">
    <button class="primary" id="btnPauseResume">Pause</button>
    <button id="btnStep">Step</button>
    <button id="btnRestart">Restart</button>
  </div>
  <div class="row">
    <button data-speed="0.5">0.5x</button>
    <button data-speed="1">1x</button>
    <button data-speed="2">2x</button>
    <button data-speed="5">5x</button>
  </div>
  <div class="row">
    <input type="number" id="jumpTick" placeholder="tick" />
    <button id="btnJump">Jump</button>
  </div>
  <div class="current" id="current" style="display:none"></div>
  <h2 style="font-size:14px;color:#9aa7bd;">Upcoming</h2>
  <ul id="upcoming"></ul>

<script>
async function post(path) {
  await fetch(path, { method: "POST" });
  refresh();
}
document.getElementById("btnStep").onclick = () => post("/_sim/step");
document.getElementById("btnRestart").onclick = () => post("/_sim/restart");
document.getElementById("btnJump").onclick = () => {
  const t = document.getElementById("jumpTick").value;
  post("/_sim/jump?tick=" + encodeURIComponent(t));
};
document.querySelectorAll("[data-speed]").forEach(b => {
  b.onclick = () => post("/_sim/speed?x=" + b.getAttribute("data-speed"));
});
let paused = false;
document.getElementById("btnPauseResume").onclick = () => post(paused ? "/_sim/resume" : "/_sim/pause");

async function refresh() {
  try {
    const r = await fetch("/_sim/status");
    const s = await r.json();
    paused = s.paused;
    document.getElementById("tick").textContent = "T" + String(s.tick).padStart(3, "0") + " / " + s.tickCount;
    document.getElementById("status").textContent =
      (s.paused ? "Paused" : "Running") + " - " + s.speed + "x - " + s.matchKey + " - routes:" + s.routes + (s.inGame ? "" : " - MATCH ENDED");
    document.getElementById("btnPauseResume").textContent = s.paused ? "Resume" : "Pause";
    const cur = document.getElementById("current");
    if (s.currentExpected) { cur.style.display = "block"; cur.textContent = "NOW: " + s.currentExpected; }
    else { cur.style.display = "none"; }
    const ul = document.getElementById("upcoming");
    ul.innerHTML = "";
    (s.upcoming || []).forEach((e, i) => {
      const li = document.createElement("li");
      if (i === 0) li.className = "now";
      li.textContent = "T" + e.tick + "  " + e.text;
      ul.appendChild(li);
    });
  } catch (e) { /* server may be between requests, just retry next tick */ }
}
setInterval(refresh, 1000);
refresh();
</script>
</body>
</html>
""";
}
