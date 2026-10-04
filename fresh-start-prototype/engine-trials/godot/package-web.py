# Packages the Godot web export (godot/build, made by `godot --headless --path godot/project --export-release Web
# ../build/index.html`) into dist/godot for sharing as an artifact, which serves scripts but not Godot's .pck
# and caps each file at 15 MB (the engine wasm is 35 MB). So, like Emscripten's single-file mode, the binaries
# ride inside scripts: the wasm gzipped (8 MB) and the pack, each as base64 in a small .js file, and a script in
# the page answers the engine's fetches of index.wasm and index.pck from them (unpacking the wasm as it goes).
# Run from engine-trials: python3 godot/package-web.py
import gzip, os, shutil
SRC, OUT = 'godot/build', 'dist/godot'
os.makedirs(OUT, exist_ok=True)
for f in ['index.js', 'index.audio.worklet.js', 'index.png']:
    shutil.copy(os.path.join(SRC, f), OUT)
import base64
def embed(name, data, var):
    with open(os.path.join(OUT, name), 'w') as f:
        f.write('window.%s = "%s";\n' % (var, base64.b64encode(data).decode()))
embed('wasm-data.js', gzip.compress(open(os.path.join(SRC, 'index.wasm'), 'rb').read(), 9), 'GODOT_WASM_GZ')
embed('pck-data.js', open(os.path.join(SRC, 'index.pck'), 'rb').read(), 'GODOT_PCK')
html = open(os.path.join(SRC, 'index.html')).read()
patch = """<script src="wasm-data.js"></script>
<script src="pck-data.js"></script>
<script>
// The engine (index.wasm, gzipped) and the game pack (index.pck) arrive as base64 in the two scripts above;
// the engine's fetches of them are answered from there
(() => {
  const bytes = b64 => Uint8Array.from(atob(b64), c => c.charCodeAt(0));
  const plain = window.fetch.bind(window);
  window.fetch = (input, init) => {
    const url = typeof input === 'string' ? input : input.url;
    if (/index\\.wasm(\\?|$)/.test(url)) {
      const body = new Blob([bytes(window.GODOT_WASM_GZ)]).stream().pipeThrough(new DecompressionStream('gzip'));
      return Promise.resolve(new Response(body, { status: 200, headers: { 'Content-Type': 'application/wasm' } }));
    }
    if (/index\\.pck(\\?|$)/.test(url)) {
      const b = bytes(window.GODOT_PCK);
      return Promise.resolve(new Response(b, { status: 200, headers: { 'Content-Type': 'application/octet-stream', 'Content-Length': String(b.length) } }));
    }
    return plain(input, init);
  };
})();
</script>
"""
html = html.replace('<script src="index.js"></script>', patch + '<script src="index.js"></script>', 1)
html = html.replace('<title>Nova Striker Godot Trial</title>', '<title>Godot Engine Trial</title>')
open(os.path.join(OUT, 'index.html'), 'w').write(html)
print('packaged', sorted(os.listdir(OUT)))
