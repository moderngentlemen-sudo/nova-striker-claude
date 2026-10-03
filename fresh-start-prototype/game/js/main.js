// Bootstrap: fixed 60 Hz simulation, interpolated rendering, drop-in joining, menus.
import { SETTINGS, loadSettings, saveSettings, DT, ROSTER, nextChar, PLAYER_COLORS } from './config.js';
import { Input } from './input.js';
import { World } from './world.js';
import { View } from './render.js';
import { UI } from './ui.js';
import { Sound } from './audio.js';
import { Music } from './music.js';
import { Haptics } from './haptics.js';
import { Bots, isBot, ORDERS } from './bot.js';
import { LANE } from './space.js';

loadSettings();
const app = document.getElementById('app');
const canvas = document.getElementById('game');
const input = new Input(canvas);
const world = new World();
const view = new View(canvas);
const sound = new Sound();
const music = new Music();
const haptics = new Haptics(input);
const bots = new Bots();
let started = false, paused = false;

const ui = new UI(document.getElementById('overlay'), {
  resume: () => setPaused(false),
  zone: id => { world.teleport(id); setPaused(false); },
  boss: id => { world.bossRush(id); setPaused(false); },
  pick: (p, c) => world.swapCharacter(p, c),
  remove: p => {
    // removing an AI teammate turns the setting down by one, so it isn't simply added back
    if (isBot(p)) { SETTINGS.aiTeammates = Math.max(0, (Number(SETTINGS.aiTeammates) || 0) - 1); saveSettings(); }
    sound.jet(p, false); world.removePlayer(p.slot);
  },
});

function resize() {
  const r = app.getBoundingClientRect();
  view.resize(Math.max(1, Math.floor(r.width)), Math.max(1, Math.floor(r.height)), world);
}
new ResizeObserver(resize).observe(app);
resize();

function setPaused(on) {
  paused = on; ui.setPaused(on, world);
  if (on) { if (document.pointerLockElement === canvas) document.exitPointerLock(); }
  else { canvas.focus(); input.swallowAll(); lockPointer(); }
}
// The mouse turns the camera while the pointer is locked to the game: a click on the game locks it, and the
// browser's own way out (Esc) pauses
function lockPointer() {
  if (!started || paused || ui.helpOpen || !world.players.some(p => p.device === 'kbm')) return;
  if (document.pointerLockElement !== canvas && canvas.requestPointerLock) { try { const r = canvas.requestPointerLock(); if (r && r.catch) r.catch(() => {}); } catch (e) { /* not allowed here */ } }
}
canvas.addEventListener('click', lockPointer);
document.addEventListener('pointerlockchange', () => {
  if (document.pointerLockElement !== canvas && started && !paused && !ui.helpOpen) setPaused(true);
});

function tryJoin() {
  const devices = input.pollJoins(new Set(world.players.map(p => p.device)));
  for (const dev of devices) {
    if (paused) break;
    if (world.players.length >= 4 && !bots.makeRoom(world)) break;   // a person joining a full team takes an AI teammate's place
    // Each new player takes the next character no one is playing yet (Nova, Echo, RAM, Fix)
    const used = new Set(world.players.map(p => p.char)), char = ROSTER.find(c => !used.has(c)) || ROSTER[world.players.length % ROSTER.length];
    world.addPlayer(dev, char);
    if (!started) { started = true; ui.hideStart(); }
  }
  if (!started && input.gamepadBlocked) ui.gamepadNotice(true);
}

// A team command to the AI teammates: they answer, and the HUD shows it while it stands
function giveOrder(p, type) {
  if (p.state === 'dead' || isBot(p)) return;
  const answers = bots.issue(world, p, type);
  if (!answers.length) { ui.toast('No AI teammates to command (Settings: AI teammates)'); return; }
  ui.toast(bots.order ? `P${p.slot + 1}: ${ORDERS.names[type]}` : `P${p.slot + 1}: back to following`);
  answers.forEach(([b, line], i) => setTimeout(() => { if (world.players.includes(b)) ui.bark(b, line); }, 150 + i * 350));
  if (bots.order) { LANE.z = p.z; view.fx.groundRing(p.x, p.y, PLAYER_COLORS[p.slot], 0.4, 2.4, 0.5, 0.85); }
}

function handleMenuEvents() {
  for (const ev of input.takeMenuEvents()) {
    // The controls screen closes with any controller's B, A, Start or View (H or Esc on the keyboard); the
    // buttons that closed it don't also act in the game
    if (started && ui.helpOpen) {
      if (['back', 'confirm', 'pause', 'help'].includes(ev.type)) { ui.toggleHelp(false); input.swallowAll(); }
      else if (ev.type === 'up' || ev.type === 'down') ui.scrollHelp(ev.type === 'down' ? 1 : -1);   // the D-pad scrolls it
      continue;
    }
    const p = world.players.find(q => q.device === ev.dev);
    if (!started || (!p && ev.type !== 'help')) continue;
    if (ev.type === 'pause') setPaused(!paused);
    else if (ev.type === 'help') { ui.toggleHelp(); if (ui.helpOpen && document.pointerLockElement === canvas) document.exitPointerLock(); }
    else if (ev.type === 'debug') ui.toggleDebug();
    else if (paused) ui.menuNav(ev);
    else if (ev.type === 'swap') world.swapCharacter(p, nextChar(p.char, ev.dir || 1));
    else if (ev.type === 'pick') world.swapCharacter(p, ev.char);
    else if (ev.type === 'order') giveOrder(p, ev.order);
  }
}

function stepSim() {
  bots.sync(world, Number(SETTINGS.aiTeammates) || 0);
  let cmds = {};
  for (const p of world.players) {
    if (isBot(p)) continue;
    // the camera turns the stick into a move along the ground and aims at what is under the crosshair
    const cmd = input.sample(p.device), cam = view.camFor(p);
    cmds[p.slot] = cam ? cam.command(cmd, world) : cmd;
  }
  bots.commands(world, cmds);
  // The command in force: on the HUD, a marker where they hold, and a word when an attack order's target falls
  const O = bots.order;
  ui.setOrder(O && world.players.includes(O.by) ? { name: ORDERS.names[O.type], slot: O.by.slot } : null);
  if (O && O.type === 'hold' && world.tick % 50 === 0) { LANE.z = O.z || 0; view.fx.groundRing(O.x, O.y, PLAYER_COLORS[O.by.slot], 0.5, 1.8, 0.45, 0.6); }
  if (bots.done === world.tick) { const b = world.players.find(isBot); if (b) ui.bark(b, ORDERS.lines[b.char].done); }
  if (window.__NS.inject) cmds = window.__NS.inject(world.tick, cmds) || cmds;
  world.step(cmds);
  for (const ev of world.events) { view.onEvent(ev); sound.play(ev); ui.onEvent(ev, world); haptics.onEvent(ev); }
  world.events.length = 0;
}

// Browsers only allow audio after a click or key press; the score starts with the first one
const unlockAudio = () => { sound.unlock(); if (sound.ctx) music.start(sound.ctx); };
window.addEventListener('pointerdown', unlockAudio);
window.addEventListener('keydown', unlockAudio);

const IDLE = { players: [] };
let acc = 0, last = performance.now(), fps = 60, fpsT = 0, fpsN = 0;
function frame(now) {
  const dt = Math.min(0.1, Math.max(0, (now - last) / 1000)); last = now;
  fpsT += dt; fpsN++; if (fpsT >= 0.5) { fps = fpsN / fpsT; fpsT = 0; fpsN = 0; }
  input.menuOpen = paused || ui.helpOpen;
  input.pollPadMenus();
  handleMenuEvents();
  tryJoin();
  const halted = paused || ui.helpOpen;   // the game waits while a menu or the controls screen is open
  // Each person turns their own camera
  if (started && !halted) for (const p of world.players) { const c = view.camFor(p); if (c) { const [yaw, pitch] = input.look(p.device, dt); c.turn(yaw, pitch); } }
  if (started && !halted && !window.__NS.manual) {
    if (view.hitPause > 0) { view.hitPause -= dt; acc = 0; }   // an impact frame's hit-pause holds the world still
    else {
      acc += dt; let steps = 0;
      while (acc >= DT && steps < 5) { stepSim(); acc -= DT; steps++; }
      if (steps === 5) acc = 0;
    }
  }
  music.update(dt, started ? world : null, halted);
  sound.update(started && !halted ? world : IDLE);   // charge hums and wall-slide grind
  if (started && !halted) haptics.update(world);
  try {
    view.render(world, halted ? 1 : Math.min(1, acc / DT), dt);
    ui.update(dt, world, view, fps);
  } catch (err) {
    console.error(err);
  }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

// Test hooks (used by automated checks; harmless otherwise)
window.__NS = {
  world, view, ui, input, music, sound, haptics, bots, SETTINGS, manual: false, inject: null,
  order(type, slot = 0) { const p = world.players.find(q => q.slot === slot); if (p) giveOrder(p, type); },
  start(char = 'nova') { if (!started) { world.addPlayer('kbm', char); started = true; ui.hideStart(); } },
  step(n = 1) { for (let i = 0; i < n; i++) stepSim(); },
  stats() { return { fps, players: world.players.length, enemies: world.enemies.length, tick: world.tick }; },
};
