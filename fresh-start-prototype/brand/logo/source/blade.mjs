import fs from 'fs';
// Blade v2 letterforms, 100-unit cap height. Rule: chamfer the top-left and bottom-right corners
// (the same diagonal as the italic) so every letter leans into the motion.
const G = {
  N: [70, [[[0,12],[12,0],[20,0],[50,60],[50,0],[70,0],[70,88],[58,100],[50,100],[20,40],[20,100],[0,100]]]],
  O: [70, [[[20,0],[70,0],[70,80],[50,100],[0,100],[0,20]], [[30,20],[50,20],[50,70],[40,80],[20,80],[20,30]]]],
  V: [72, [[[0,0],[21,0],[36,66],[51,0],[72,0],[47,100],[25,100]]]],
  A: [72, [[[25,0],[47,0],[72,100],[51,100],[47,82],[25,82],[21,100],[0,100]], [[29,64],[43,64],[36,34]]]],
  Λ: [72, [[[27,0],[45,0],[72,100],[51,100],[36,40],[21,100],[0,100]]]],
  S: [66, [[[12,0],[66,0],[62,20],[20,20],[20,40],[54,40],[66,52],[66,88],[54,100],[0,100],[4,80],[46,80],[46,60],[12,60],[0,48],[0,12]]]],
  T: [66, [[[0,0],[66,0],[60,20],[43,20],[43,100],[23,100],[23,20],[6,20]]]],
  R: [68, [[[12,0],[56,0],[68,12],[68,46],[58,56],[68,100],[47,100],[38,60],[20,60],[20,100],[0,100],[0,12]], [[20,20],[48,20],[48,40],[20,40]]]],
  // Final R: leg drops through the baseline into the underline blade.
  Ʀ: [68, [[[12,0],[56,0],[68,12],[68,46],[58,56],[94,116],[71,116],[38,60],[20,60],[20,100],[0,100],[0,12]], [[20,20],[48,20],[48,40],[20,40]]]],
  I: [20, [[[0,12],[12,0],[20,0],[20,100],[0,100]]]],
  K: [68, [[[0,12],[12,0],[20,0],[20,40],[44,0],[68,0],[37,50],[68,100],[45,100],[20,60],[20,100],[0,100]]]],
  E: [60, [[[12,0],[60,0],[56,20],[20,20],[20,40],[50,40],[46,58],[20,58],[20,80],[60,80],[60,88],[56,100],[0,100],[0,12]]]],
};
const KERN = { VA: -16, VΛ: -16, AV: -16, TR: -4, KE: -4 };
function word(txt, gap = 7, x0 = 0) {
  let x = x0, out = [], prev = '';
  for (const ch of txt) {
    if (ch === ' ') { x += 34; prev = ''; continue; }
    x += KERN[prev + ch] || 0; prev = ch;
    const [w, polys] = G[ch];
    out.push({ ch, x, w, d: polys.map(p => 'M' + p.map(q => (q[0] + x) + ',' + q[1]).join('L') + 'Z').join('') });
    x += w + gap;
  }
  return { glyphs: out, width: x - gap - x0 };
}
const paths = (gs, fill) => gs.map(g => `<path fill-rule="evenodd" d="${g.d}" fill="${fill}"/>`).join('');
let uid = 0;
const PAL = {
  core:  { nova: ['#ffffff', '#9fecff', '#1f7cff'], strk: ['#ffffff', '#d9e4f2', '#7d8ea6'], glowN: '#2fb8ff', glowS: '#ff2e7e', acc: '#ff2e7e', bg: '#0b1322' },
  heat:  { nova: ['#fff4dc', '#ffb547', '#e2561f'], strk: ['#ffffff', '#d9e4f2', '#7d8ea6'], glowN: '#ff8a2e', glowS: '#ff2e7e', acc: '#ff2e7e', bg: '#0b1322' },
  mono:  { nova: ['#f3f7fc', '#f3f7fc', '#f3f7fc'], strk: ['#f3f7fc', '#f3f7fc', '#f3f7fc'], acc: '#f3f7fc', bg: '#0b1322' },
  light: { nova: ['#1f7cff', '#1f7cff', '#0b3f99'], strk: ['#0b1322', '#0b1322', '#0b1322'], acc: '#ff2e7e', bg: '#eef3f9' },
};
function grad(id, c) { return `<linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${c[0]}"/><stop offset=".5" stop-color="${c[1]}"/><stop offset="1" stop-color="${c[2]}"/></linearGradient>`; }

// Horizontal lockup. opts: delta (Λ for A), flourish (R leg + underline blade), pal.
export function horizontal({ delta = false, flourish = true, pal = 'core', glow = true } = {}) {
  const p = PAL[pal], u = ++uid;
  const txt = `NOV${delta ? 'Λ' : 'A'} STRIKE${flourish ? 'Ʀ' : 'R'}`;
  const w = word(txt), nova = w.glyphs.slice(0, 4), strk = w.glyphs.slice(4), W = w.width;
  const last = strk[strk.length - 1];
  const cutY = 46; // speed cut: thin gap, rising slightly left→right
  const under = flourish
    ? `<polygon points="-24,113 ${last.x + 64},106 ${last.x + 100},106 ${last.x + 96},116 ${last.x + 64},116" fill="url(#ul${u})"/>`
    : `<polygon points="0,114 ${W},114 ${W - 6},120 -6,120" fill="${p.acc}"/>`;
  return { W: W + (flourish ? 150 : 120), H: 172, svg: `<defs>${grad('n' + u, p.nova)}${grad('s' + u, p.strk)}
    <linearGradient id="ul${u}" x1="0" x2="1"><stop offset="0" stop-color="${p.acc}" stop-opacity="0"/><stop offset=".55" stop-color="${p.acc}"/><stop offset="1" stop-color="${p.strk[1]}"/></linearGradient>
    <mask id="m${u}"><rect x="-80" y="-60" width="${W + 200}" height="260" fill="#fff"/><polygon points="-60,${cutY + 4} ${W + 80},${cutY - 3} ${W + 80},${cutY + 2} -60,${cutY + 9}" fill="#000"/></mask>
    <filter id="g${u}" x="-10%" y="-30%" width="120%" height="160%"><feGaussianBlur stdDeviation="6"/></filter></defs>
    <g transform="translate(46 30) skewX(-14)">
      <g mask="url(#m${u})">
        ${glow && p.glowN ? `<g filter="url(#g${u})" opacity=".55">${paths(nova, p.glowN)}${paths(strk, p.glowS)}</g>` : ''}
        ${paths(nova, `url(#n${u})`)}${paths(strk, `url(#s${u})`)}
      </g>
      <polygon points="-34,${cutY + 2} -8,${cutY + 1} -11,${cutY + 6} -37,${cutY + 7}" fill="${p.acc}"/>
      ${flourish ? '' : `<polygon points="${W + 8},${cutY - 3} ${W + 40},${cutY - 4} ${W + 37},${cutY + 1} ${W + 5},${cutY + 2}" fill="${p.acc}"/>`}
      ${under}
    </g>` };
}
// Stacked lockup: NOVA big, STRIKER scaled to the same width underneath.
export function stacked({ pal = 'core' } = {}) {
  const p = PAL[pal], u = ++uid;
  const nova = word('NOVA', 9), strk = word('STRIKER', 6), s = nova.width / strk.width;
  return { W: nova.width + 120, H: 100 + 100 * s + 90, svg: `<defs>${grad('n' + u, p.nova)}${grad('s' + u, p.strk)}
    <mask id="m${u}"><rect x="-80" y="-60" width="600" height="400" fill="#fff"/><polygon points="-60,50 ${nova.width + 80},43 ${nova.width + 80},48 -60,55" fill="#000"/></mask>
    <filter id="g${u}"><feGaussianBlur stdDeviation="6"/></filter></defs>
    <g transform="translate(60 26) skewX(-14)">
      <g mask="url(#m${u})"><g filter="url(#g${u})" opacity=".5">${paths(nova.glyphs, p.glowN)}</g>${paths(nova.glyphs, `url(#n${u})`)}</g>
      <polygon points="-34,48 -8,47 -11,52 -37,53" fill="${p.acc}"/>
      <polygon points="0,112 ${nova.width},110 ${nova.width - 2},114 -2,116" fill="${p.acc}"/>
      <g transform="translate(0 126) scale(${s})">${paths(strk.glyphs, `url(#s${u})`)}</g>
    </g>` };
}
// Icon: N in a raked plate with the speed cut.
export function icon({ pal = 'core', round = false } = {}) {
  const p = PAL[pal], u = ++uid, n = word('N');
  return { W: 200, H: 200, svg: `<defs>${grad('n' + u, p.nova)}
    <linearGradient id="bg${u}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#16274a"/><stop offset="1" stop-color="#070c16"/></linearGradient>
    <mask id="m${u}"><rect width="300" height="300" x="-50" y="-50" fill="#fff"/><polygon points="-40,50 120,45 120,50 -40,55" fill="#000"/></mask>
    <filter id="g${u}"><feGaussianBlur stdDeviation="5"/></filter></defs>
    ${round ? `<rect width="200" height="200" rx="44" fill="url(#bg${u})"/><rect x="1" y="1" width="198" height="198" rx="43" fill="none" stroke="#ffffff22"/>` : ''}
    <g transform="translate(${round ? 62 : 58} ${round ? 46 : 42}) scale(1.08) skewX(-14)">
      ${round ? '' : `<polygon points="-22,-14 92,-14 92,96 78,114 -22,114" fill="${p.bg}" stroke="${p.acc}" stroke-width="3"/>`}
      <g mask="url(#m${u})"><g filter="url(#g${u})" opacity=".6">${paths(n.glyphs, p.glowN || p.nova[1])}</g>${paths(n.glyphs, `url(#n${u})`)}</g>
      <polygon points="-34,52 -8,51 -11,56 -37,57" fill="${p.acc}"/>
      <polygon points="0,${round ? 108 : 104} 70,${round ? 107 : 103} 68,${round ? 111 : 107} -2,${round ? 112 : 108}" fill="${p.acc}"/>
    </g>` };
}
export const svg = (o, extra = '') => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${o.W} ${o.H}" ${extra}>${o.svg}</svg>`;
