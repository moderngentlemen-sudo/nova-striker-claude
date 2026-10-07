// Builds every Blade logo variant into ../svg and ../png.
// Run: node build.mjs   (needs Playwright for the PNGs, installed locally or globally)
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { createRequire } from 'module';
import { execSync } from 'child_process';
import { horizontal, stacked, icon, svg } from './blade.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const variants = {
  'nova-striker-hero': [horizontal(), [1600, 800]],
  'nova-striker-hero-delta': [horizontal({ delta: true }), [1600]],
  'nova-striker-hero-plain': [horizontal({ flourish: false }), [1600]],
  'nova-striker-hero-heat': [horizontal({ pal: 'heat' }), [1600]],
  'nova-striker-hero-mono': [horizontal({ pal: 'mono', glow: false }), [1600]],
  'nova-striker-hero-light': [horizontal({ pal: 'light', glow: false }), [1600]],
  'nova-striker-stacked': [stacked(), [1200]],
  'nova-striker-icon-plate': [icon(), [512]],
  'nova-striker-icon-app': [icon({ round: true }), [1024, 512, 192, 64, 32]],
};
fs.mkdirSync(path.join(root, 'svg'), { recursive: true });
fs.mkdirSync(path.join(root, 'png'), { recursive: true });
for (const [name, [o]] of Object.entries(variants)) fs.writeFileSync(path.join(root, 'svg', name + '.svg'), svg(o) + '\n');

let chromium;
try { ({ chromium } = await import('playwright')); }
catch { ({ chromium } = createRequire(path.join(execSync('npm root -g').toString().trim(), 'x'))('playwright')); }
const browser = await chromium.launch();
const page = await browser.newPage();
for (const [name, [o, widths]] of Object.entries(variants)) {
  for (const w of widths) {
    const h = Math.round(w * o.H / o.W);
    await page.setViewportSize({ width: w, height: h });
    await page.setContent(`<style>html,body{margin:0;background:transparent}svg{display:block}</style>${svg(o, `width="${w}" height="${h}"`)}`);
    await page.screenshot({ path: path.join(root, 'png', `${name}-${w}.png`), omitBackground: true });
  }
}
await browser.close();
console.log('Built', Object.keys(variants).length, 'variants into', root);
