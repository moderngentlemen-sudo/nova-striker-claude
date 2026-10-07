// Opens the preview page headless and saves a screenshot. Usage: node shot.cjs <page.html> <out.png>
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined, args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await b.newPage({ viewport: { width: 2100, height: 760 } });
  p.on('console', m => console.log('console:', m.text())); p.on('pageerror', e => console.log('pageerror:', e.message));
  await p.goto('file://' + process.argv[2]); await p.waitForFunction('window.DONE === true', null, { timeout: 60000 });
  await p.screenshot({ path: process.argv[3] }); await b.close();
})();
