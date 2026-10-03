// Input: keyboard + mouse (one device) and up to four gamepads.
// Each simulation tick, a device produces one command frame with held/pressed/released edges: the move stick as
// (sx right, sy forward), crouch, and the buttons. Its player's camera (camera.js) turns that into a move along
// the ground and an aim. Every frame, look(dev, dt) says how far a device turned its camera: the mouse (with the
// pointer locked to the game), the arrow keys, or the right stick.
import { SETTINGS } from './config.js';

const BTNS = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];

const ORDER_HOLD = 380;   // ms of D-pad up/down held for the second command (Cover me, Hold here)
const KEYMAP = {
  Space: 'jump', ShiftLeft: 'dash', ShiftRight: 'dash',
  KeyJ: 'melee', KeyK: 'fire', KeyL: 'parry', KeyQ: 'parry', KeyE: 'sig', KeyI: 'sig',
  KeyR: 'mode', KeyU: 'mode',   // Echo: cycle scarf mode; Nova: cycle bracer attachment
  KeyF: 'lock', KeyO: 'lock',   // lock-on (the mouse's forward button too)
  KeyT: 'sub', KeyY: 'sub',     // Nova: switch secondary weapon · RAM: Provoke · Fix: switch power-up
  KeyV: 'ult', KeyN: 'ult',     // ultimate (a gamepad pulls both triggers)
};

function deadzone(x, y, dz) {
  const m = Math.hypot(x, y);
  if (m < dz) return [0, 0];
  const s = Math.min(1, (m - dz) / (1 - dz)) / m;
  return [x * s, y * s];
}

export class Input {
  constructor(canvas) {
    this.canvas = canvas;
    this.keys = new Set();
    this.kbPressed = new Set();    // presses since last sample (so fast taps aren't lost)
    this.kbReleased = new Set();
    this.mouse = { x: 0, y: 0, moved: false, buttons: 0 };
    this.mousePressed = new Set();
    this.mouseReleased = new Set();
    this.prevPads = {};
    this.devices = {};             // deviceId -> { prevHeld, freeAimGrace }
    this.gamepadBlocked = false;
    this.menuEvents = [];          // pause/help/debug/swap toggles for the UI
    this.menuOpen = false;         // a menu is up: the stick navigates it, and held directions repeat
    this.repeat = {};              // per pad: when each held direction fires again
    this.anyKbm = false;           // any keyboard/mouse input since last join poll

    window.addEventListener('keydown', e => {
      if (['Space', 'Tab', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault();
      if (e.repeat) return;
      this.keys.add(e.code);
      this.kbPressed.add(e.code);
      this.anyKbm = true;
      if (e.code === 'Escape' || e.code === 'KeyP') this.menuEvents.push({ dev: 'kbm', type: 'pause' });
      if (e.code === 'KeyH') this.menuEvents.push({ dev: 'kbm', type: 'help' });
      if (e.code === 'Backquote') this.menuEvents.push({ dev: 'kbm', type: 'debug' });
      if (e.code === 'Tab') this.menuEvents.push({ dev: 'kbm', type: 'swap', dir: 1 });
      if (e.code === 'Digit1') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'nova' });
      if (e.code === 'Digit2') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'echo' });
      if (e.code === 'Digit3') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'ram' });
      if (e.code === 'Digit4') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'fix' });
      // Team commands to the AI teammates (bot.js): Z attack my target · G cover me · X regroup on me · B hold here
      const order = { KeyZ: 'attack', KeyG: 'cover', KeyX: 'regroup', KeyB: 'hold' }[e.code];
      if (order) this.menuEvents.push({ dev: 'kbm', type: 'order', order });
    });
    window.addEventListener('keyup', e => {
      this.keys.delete(e.code);
      this.kbReleased.add(e.code);
    });
    window.addEventListener('blur', () => { this.keys.clear(); this.mouse.buttons = 0; });
    this.lookDX = 0; this.lookDY = 0;   // mouse movement while the pointer is locked to the game
    document.addEventListener('mousemove', e => {
      if (document.pointerLockElement === canvas) { this.lookDX += e.movementX || 0; this.lookDY += e.movementY || 0; }
      const r = canvas.getBoundingClientRect();
      this.mouse.x = e.clientX - r.left; this.mouse.y = e.clientY - r.top; this.mouse.moved = true;
    });
    canvas.addEventListener('mousedown', e => {
      if (e.button >= 3) e.preventDefault();   // back/forward buttons are game buttons here, not navigation
      this.mouse.buttons |= (1 << e.button);
      this.mousePressed.add(e.button);
      this.anyKbm = true;
    });
    window.addEventListener('mouseup', e => {
      if (e.button >= 3 && e.target === canvas) e.preventDefault();
      this.mouse.buttons &= ~(1 << e.button);
      this.mouseReleased.add(e.button);
    });
    canvas.addEventListener('contextmenu', e => e.preventDefault());
    // A click anywhere (including on the start card) counts as keyboard + mouse wanting to join
    window.addEventListener('pointerdown', () => { this.anyKbm = true; });
  }

  pads() {
    if (this.gamepadBlocked) return [];
    try {
      return Array.from(navigator.getGamepads ? navigator.getGamepads() : []).filter(Boolean);
    } catch (e) {
      this.gamepadBlocked = true;
      return [];
    }
  }

  // Devices that pressed something this frame and are not yet assigned.
  pollJoins(assigned) {
    const out = [];
    if (this.anyKbm && !assigned.has('kbm')) out.push('kbm');
    this.anyKbm = false;
    for (const p of this.pads()) {
      const id = 'pad' + p.index;
      if (assigned.has(id)) continue;
      if (p.buttons.some(b => b.pressed)) out.push(id);
    }
    return out;
  }

  // Pad menu buttons (Start/View/D-pad) become UI events. While a menu is open (menuOpen, set by main) the
  // left stick moves through it too, and a direction held on the D-pad or stick repeats after a short pause.
  pollPadMenus() {
    const t = performance.now();
    for (const p of this.pads()) {
      const id = 'pad' + p.index;
      const prev = this.prevPads[id] || [];
      const now = p.buttons.map(b => b.pressed);
      const edge = i => now[i] && !prev[i];
      if (edge(9)) this.menuEvents.push({ dev: id, type: 'pause' });
      if (edge(8)) this.menuEvents.push({ dev: id, type: 'help' });
      if (edge(0)) this.menuEvents.push({ dev: id, type: 'confirm' });
      if (edge(1)) this.menuEvents.push({ dev: id, type: 'back' });
      if (edge(4)) this.menuEvents.push({ dev: id, type: 'prevTab' });
      if (edge(5)) this.menuEvents.push({ dev: id, type: 'nextTab' });
      const sx = this.menuOpen ? p.axes[0] || 0 : 0, sy = this.menuOpen ? p.axes[1] || 0 : 0;
      const dirs = [
        ['up', now[12] || sy < -0.6, { type: 'up' }], ['down', now[13] || sy > 0.6, { type: 'down' }],
        ['left', now[14] || sx < -0.6, { type: 'swap', dir: -1 }], ['right', now[15] || sx > 0.6, { type: 'swap', dir: 1 }],
      ];
      const R = this.repeat[id] || (this.repeat[id] = {});
      // In play, D-pad up and down are team commands: a tap of up is Attack my target and holding it Cover me;
      // a tap of down is Regroup on me and holding it Hold here (ORDER_HOLD ms)
      if (!this.menuOpen) {
        for (const [btn, tap, hold] of [[12, 'attack', 'cover'], [13, 'regroup', 'hold']]) {
          const k = 'o' + btn, r = R[k];
          if (now[btn] && !r) R[k] = { t0: t, done: false };
          else if (now[btn] && r && !r.done && t - r.t0 >= ORDER_HOLD) { r.done = true; this.menuEvents.push({ dev: id, type: 'order', order: hold }); }
          else if (!now[btn] && r) { if (!r.done) this.menuEvents.push({ dev: id, type: 'order', order: tap }); R[k] = null; }
        }
        dirs.splice(0, 2);   // (so they are not menu directions too)
      }
      for (const [k, on, ev] of dirs) {
        const r = R[k];
        if (!on) { R[k] = null; continue; }
        if (!r) { R[k] = { next: t + 320 }; this.menuEvents.push({ dev: id, ...ev }); }
        else if (this.menuOpen && t >= r.next) { r.next = t + 110; this.menuEvents.push({ dev: id, ...ev, repeat: true }); }
      }
      this.prevPads[id] = now;
    }
  }

  // How far a device turns its camera this frame, as [yaw right, pitch up] in radians
  look(dev, dt) {
    const k = Number(SETTINGS.camSens) || 1, inv = SETTINGS.invertY ? -1 : 1;
    if (dev === 'kbm') {
      const key = c => (this.keys.has(c) ? 1 : 0);
      let yaw = this.lookDX * 0.0024 * k, pitch = -this.lookDY * 0.0024 * k * inv;
      this.lookDX = 0; this.lookDY = 0;
      yaw += (key('ArrowRight') - key('ArrowLeft')) * 2.4 * k * dt;
      pitch += (key('ArrowUp') - key('ArrowDown')) * 1.6 * k * dt * inv;
      return [yaw, pitch];
    }
    const pad = this.pads().find(p => 'pad' + p.index === dev);
    if (!pad || this.menuOpen) return [0, 0];
    const [rx, ry] = deadzone(pad.axes[2] || 0, -(pad.axes[3] || 0), 0.16), m = Math.hypot(rx, ry);
    if (!m) return [0, 0];
    const curve = Math.pow(m, 1.7) / m;   // fine control near the centre, a fast turn at the edge
    return [rx * curve * 3.4 * k * dt, ry * curve * 2.2 * k * dt * inv];
  }

  sample(dev) {
    const st = this.devices[dev] || (this.devices[dev] = { prevHeld: {} });
    const held = {};
    let sx = 0, sy = 0, crouch = false;

    if (dev === 'kbm') {
      const k = c => this.keys.has(c);
      sx = (k('KeyD') ? 1 : 0) - (k('KeyA') ? 1 : 0);
      sy = (k('KeyW') ? 1 : 0) - (k('KeyS') ? 1 : 0);
      if (sx && sy) { sx *= Math.SQRT1_2; sy *= Math.SQRT1_2; }
      crouch = k('KeyC');
      for (const b of BTNS) held[b] = false;
      for (const [code, b] of Object.entries(KEYMAP)) if (k(code)) held[b] = true;
      // Bits: 1 = left (fire), 2 = middle (signature), 4 = right (melee), 8 = back (mode), 16 = forward (lock-on)
      if (this.mouse.buttons & 1) held.fire = true;
      if (this.mouse.buttons & 2) held.sig = true;
      if (this.mouse.buttons & 4) held.melee = true;
      if (this.mouse.buttons & 8) held.mode = true;
      if (this.mouse.buttons & 16) held.lock = true;
      // Keyboard presses that happened and released between samples still count as presses
      const pressedExtra = {};
      for (const code of this.kbPressed) { const b = KEYMAP[code]; if (b) pressedExtra[b] = true; }
      if (this.mousePressed.has(0)) pressedExtra.fire = true;
      if (this.mousePressed.has(2)) pressedExtra.melee = true;
      if (this.mousePressed.has(1)) pressedExtra.sig = true;
      if (this.mousePressed.has(3)) pressedExtra.mode = true;
      if (this.mousePressed.has(4)) pressedExtra.lock = true;
      this.kbPressed.clear(); this.mousePressed.clear(); this.kbReleased.clear(); this.mouseReleased.clear();
      return this.finish(st, held, pressedExtra, sx, sy, crouch);
    }

    const pad = this.pads().find(p => 'pad' + p.index === dev);
    for (const b of BTNS) held[b] = false;
    if (pad) {
      const bt = i => (pad.buttons[i] ? pad.buttons[i].pressed || pad.buttons[i].value > 0.5 : false);
      [sx, sy] = deadzone(pad.axes[0] || 0, -(pad.axes[1] || 0), 0.22);
      crouch = bt(10);      // hold the left stick in to crouch
      held.jump = bt(0);
      held.sub = bt(4);     // LB: switch secondary weapon
      held.dash = bt(1);
      held.melee = bt(2);
      held.sig = bt(3);
      held.mode = bt(5);
      held.parry = pad.buttons[6] ? pad.buttons[6].value > 0.5 || pad.buttons[6].pressed : false;
      held.fire = pad.buttons[7] ? pad.buttons[7].value > 0.35 || pad.buttons[7].pressed : false;
      held.lock = bt(11);   // right stick click
    }
    return this.finish(st, held, {}, sx, sy, crouch);
  }

  // After a menu closes, buttons still held from closing it count as already held (no stray jump or dash)
  swallowAll() { for (const st of Object.values(this.devices)) st.swallow = true; this.kbPressed.clear(); this.mousePressed.clear(); }

  finish(st, held, pressedExtra, sx, sy, crouch) {
    if (st.swallow) { st.swallow = false; st.prevHeld = { ...held }; pressedExtra = {}; }
    const pressed = {}, released = {};
    for (const b of BTNS) {
      pressed[b] = (held[b] && !st.prevHeld[b]) || !!pressedExtra[b];
      released[b] = !held[b] && !!st.prevHeld[b];
    }
    st.prevHeld = { ...held };
    return { sx, sy, crouch, mx: 0, mz: 0, my: crouch ? -1 : 0, aimFree: false, ax: 0, ay: 0, az: 0, held, pressed, released };
  }

  takeMenuEvents() {
    const ev = this.menuEvents;
    this.menuEvents = [];
    return ev;
  }
}

const none = () => Object.fromEntries(BTNS.map(b => [b, false]));
export const EMPTY_CMD = { mx: 0, my: 0, mz: 0, aimFree: false, ax: 0, ay: 0, az: 0, held: none(), pressed: none(), released: none() };
