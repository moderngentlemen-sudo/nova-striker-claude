// Runs a scenario in the JavaScript prototype and prints the world's state every tick, one JSON line per tick.
// The C# port runs the same scenario (Trace.cs) and compare.py checks the two tick by tick.
// Usage: node trace.mjs <scenario> <ticks> <seed>
const [, , scenario = 'gym-nova', ticksArg = '600', seedArg = '1'] = process.argv;
// The prototype's Math.random becomes the same seeded generator the C# port uses (mulberry32)
let rs = Number(seedArg) >>> 0;
Math.random = () => { rs = (rs + 0x6D2B79F5) | 0; let t = Math.imul(rs ^ (rs >>> 15), 1 | rs); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
const { World } = await import('../../game/js/world.js');
const { SETTINGS } = await import('../../game/js/config.js');
const { Bots } = await import('../../game/js/bot.js');
const { GATES } = await import('../../game/js/level.js');

const BT = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];
// A scripted person: xorshift32 picks a stick direction and buttons, changing every few ticks
function person(seed) {
  let s = (seed * 2654435761) >>> 0 || 1, prev = {}, hold = 0, mx = 0, my = 0, want = {};
  const next = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s; };
  return () => {
    if (hold <= 0) {
      hold = 4 + next() % 30;
      const d = next() % 10;
      mx = [0, 1, 1, 1, 0, -1, -1, -1, 1, 0][d]; my = [0, 0, 1, -1, 1, 0, 1, -1, 0, -1][d];
      want = {}; for (let i = 0; i < BT.length; i++) want[BT[i]] = next() % 100 < [22, 10, 25, 30, 12, 6, 3, 5, 4, 2][i];
    }
    hold--;
    const held = {}, pressed = {}, released = {};
    for (const b of BT) { held[b] = !!want[b] && (b !== 'fire' || hold % 40 !== 0); pressed[b] = held[b] && !prev[b]; released[b] = !held[b] && !!prev[b]; }
    prev = held;
    return { mx, my, aimFree: false, ax: 0, ay: 0, held, pressed, released };
  };
}

const [zone, who, botsArg] = scenario.split('-');
SETTINGS.novaKit = 'marksman'; SETTINGS.echoKit = 'hunter'; SETTINGS.lockMode = 'auto'; SETTINGS.difficulty = 'normal';
SETTINGS.aiSkill = 'elite'; SETTINGS.barks = true; SETTINGS.dashCharge = true; SETTINGS.lockOn = true;
// Variant 'v': the other kits and settings (Sentinel Nova, Pursuit Echo, belt on LB, manual lock-on, hard, rookie bots)
const variant = scenario.split('-')[3];
if (variant === 'v') { SETTINGS.novaKit = 'sentinel'; SETTINGS.echoKit = 'pursuit'; SETTINGS.echoRanged = 'B'; SETTINGS.echoBelt = 'lb'; SETTINGS.lockMode = 'manual';
  SETTINGS.difficulty = 'hard'; SETTINGS.aiSkill = 'rookie'; SETTINGS.dashIframes = true; SETTINGS.vbStop = 'keep30'; SETTINGS.dashCharge = false; }
if (variant === 'w') { SETTINGS.echoBelt = 'lb'; SETTINGS.echoRanged = 'A'; SETTINGS.difficulty = 'hard'; SETTINGS.aiSkill = 'veteran'; SETTINGS.echoSpinStun = 2.5; SETTINGS.aimAssist = false; }
const w = new World();
if (zone !== 'warden' && zone !== 'beacon') w.teleport(zone);
const me = w.addPlayer('kbm', who);
const nb = Number(botsArg || 0), bots = new Bots(), pad = person(Number(seedArg));
if (zone === 'warden' || zone === 'beacon') w.bossRush(zone === 'warden' ? 'warden' : 'beacon');
const r = v => v;
const out = [];
for (let t = 0; t < Number(ticksArg); t++) {
  const cmds = { [me.slot]: pad() };
  bots.sync(w, nb); bots.commands(w, cmds);
  w.step(cmds);
  const ev = w.events.map(e => e.type); w.events.length = 0;
  out.push(JSON.stringify({
    t: w.tick, seq: w.instanceSeq, cp: w.checkpoint,
    P: w.players.map(p => [p.slot, p.char, p.state, p.st, p.x, p.y, p.vx, p.vy, p.hp, p.facing, p.chargeT, p.ult, p.plate, p.aimX, p.aimY]),
    E: w.enemies.map(e => [e.id, e.type, e.state, e.st, e.x, e.y, e.vx, e.vy, e.hp === Infinity ? 'inf' : e.hp, e.poise]),
    R: w.projectiles.map(q => [q.kind, q.x, q.y]),
    ev,
  }));
}
process.stdout.write(out.join('\n') + '\n');
