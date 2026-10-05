// Input: keyboard + mouse (one device) and up to four gamepads.
// Each simulation tick, a device produces one command frame with held/pressed/released edges.

const BTNS = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];

const ORDER_HOLD = 380;   // ms of D-pad up/down held for the second command (Cover me, Hold here)
const KEYMAP = {
  Space: 'jump', ShiftLeft: 'dash', ShiftRight: 'dash',
  KeyJ: 'melee', KeyK: 'fire', KeyL: 'parry', KeyQ: 'parry', KeyE: 'sig', KeyI: 'sig',
  KeyR: 'mode', KeyU: 'mode',   // Echo: cycle scarf mode; Nova: cycle bracer attachment
  KeyF: 'lock', KeyO: 'lock',   // lock-on
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
    this.anyKey = false;           // any key since last join poll (the autoplay demo joins on keys, not clicks)
    this.keysOnly = false;         // (set by main in the autoplay demo: a click only turns the sound on)

    window.addEventListener('keydown', e => {
      if (['Space', 'Tab', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault();
      if (e.repeat) return;
      this.keys.add(e.code);
      this.kbPressed.add(e.code);
      this.anyKbm = true; this.anyKey = true;
      if (e.code === 'Escape' || e.code === 'KeyP') this.menuEvents.push({ dev: 'kbm', type: 'pause' });
      if (e.code === 'KeyH') this.menuEvents.push({ dev: 'kbm', type: 'help' });
      if (e.code === 'Backquote') this.menuEvents.push({ dev: 'kbm', type: 'debug' });
      if (e.code === 'Tab') this.menuEvents.push({ dev: 'kbm', type: 'swap', dir: 1 });
      if (e.code === 'Digit1') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'nova' });
      if (e.code === 'Digit2') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'echo' });
      if (e.code === 'Digit3') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'ram' });
      if (e.code === 'Digit4') this.menuEvents.push({ dev: 'kbm', type: 'pick', char: 'fix' });
      // Team commands to the AI teammates (bot.js): Z attack my target · G cover me · X regroup on me · C hold here
      const order = { KeyZ: 'attack', KeyG: 'cover', KeyX: 'regroup', KeyC: 'hold' }[e.code];
      if (order) this.menuEvents.push({ dev: 'kbm', type: 'order', order });
    });
    window.addEventListener('keyup', e => {
      this.keys.delete(e.code);
      this.kbReleased.add(e.code);
    });
    window.addEventListener('blur', () => { this.keys.clear(); this.mouse.buttons = 0; });
    canvas.addEventListener('mousemove', e => {
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
    if ((this.keysOnly ? this.anyKey : this.anyKbm) && !assigned.has('kbm')) out.push('kbm');
    this.anyKbm = false; this.anyKey = false;
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

  // aimFromMouse(screenX, screenY) is supplied by the caller: returns a unit sim-space vector.
  sample(dev, aimFromMouse, p1AimMode) {
    const st = this.devices[dev] || (this.devices[dev] = { prevHeld: {}, grace: 0, lastFree: [1, 0] });
    const held = {};
    let mx = 0, my = 0, aimFree = false, ax = 0, ay = 0;

    if (dev === 'kbm') {
      const k = c => this.keys.has(c);
      mx = (k('KeyD') || k('ArrowRight') ? 1 : 0) - (k('KeyA') || k('ArrowLeft') ? 1 : 0);
      my = (k('KeyW') || k('ArrowUp') ? 1 : 0) - (k('KeyS') || k('ArrowDown') ? 1 : 0);
      for (const b of BTNS) held[b] = false;
      for (const [code, b] of Object.entries(KEYMAP)) if (k(code)) held[b] = true;
      // Bits: 1 = left (fire), 2 = middle (signature), 4 = right (melee), 8 = back (mode), 16 = forward (lock-on)
      if (this.mouse.buttons & 1) held.fire = true;
      if (this.mouse.buttons & 2) held.sig = true;
      if (this.mouse.buttons & 4) held.melee = true;
      if (this.mouse.buttons & 8) held.mode = true;
      if (this.mouse.buttons & 16) held.lock = true;
      if (p1AimMode === 'mouse' && aimFromMouse) {
        const v = aimFromMouse(this.mouse.x, this.mouse.y);
        if (v) { aimFree = true; ax = v[0]; ay = v[1]; }
      }
      // Keyboard presses that happened and released between samples still count as presses
      const pressedExtra = {};
      for (const code of this.kbPressed) { const b = KEYMAP[code]; if (b) pressedExtra[b] = true; }
      if (this.mousePressed.has(0)) pressedExtra.fire = true;
      if (this.mousePressed.has(2)) pressedExtra.melee = true;
      if (this.mousePressed.has(1)) pressedExtra.sig = true;
      if (this.mousePressed.has(3)) pressedExtra.mode = true;
      if (this.mousePressed.has(4)) pressedExtra.lock = true;
      this.kbPressed.clear(); this.mousePressed.clear(); this.kbReleased.clear(); this.mouseReleased.clear();
      return this.finish(st, held, pressedExtra, mx, my, aimFree, ax, ay);
    }

    const pad = this.pads().find(p => 'pad' + p.index === dev);
    for (const b of BTNS) held[b] = false;
    if (pad) {
      const bt = i => (pad.buttons[i] ? pad.buttons[i].pressed || pad.buttons[i].value > 0.5 : false);
      [mx, my] = deadzone(pad.axes[0] || 0, -(pad.axes[1] || 0), 0.22);
      const [rx, ry] = deadzone(pad.axes[2] || 0, -(pad.axes[3] || 0), 0.3);
      if (bt(12)) my = 1; if (bt(13)) my = -1;
      held.jump = bt(0);
      held.sub = bt(4);     // LB: switch secondary weapon
      held.dash = bt(1);
      held.melee = bt(2);
      held.sig = bt(3);
      held.mode = bt(5);
      held.parry = pad.buttons[6] ? pad.buttons[6].value > 0.5 || pad.buttons[6].pressed : false;
      held.fire = pad.buttons[7] ? pad.buttons[7].value > 0.35 || pad.buttons[7].pressed : false;
      held.lock = bt(11);   // right stick click
      const rm = Math.hypot(rx, ry);
      if (rm > 0.35) {
        aimFree = true; ax = rx / rm; ay = ry / rm; st.grace = 18; st.lastFree = [ax, ay];
      } else if (st.grace > 0) {
        st.grace--; aimFree = true; [ax, ay] = st.lastFree;
      }
    }
    return this.finish(st, held, {}, mx, my, aimFree, ax, ay);
  }

  // After a menu closes, buttons still held from closing it count as already held (no stray jump or dash)
  swallowAll() { for (const st of Object.values(this.devices)) st.swallow = true; this.kbPressed.clear(); this.mousePressed.clear(); }

  finish(st, held, pressedExtra, mx, my, aimFree, ax, ay) {
    if (st.swallow) { st.swallow = false; st.prevHeld = { ...held }; pressedExtra = {}; }
    const pressed = {}, released = {};
    for (const b of BTNS) {
      pressed[b] = (held[b] && !st.prevHeld[b]) || !!pressedExtra[b];
      released[b] = !held[b] && !!st.prevHeld[b];
    }
    st.prevHeld = { ...held };
    return { mx, my, aimFree, ax, ay, held, pressed, released };
  }

  takeMenuEvents() {
    const ev = this.menuEvents;
    this.menuEvents = [];
    return ev;
  }
}

const none = () => Object.fromEntries(BTNS.map(b => [b, false]));
export const EMPTY_CMD = { mx: 0, my: 0, aimFree: false, ax: 0, ay: 0, held: none(), pressed: none(), released: none() };
