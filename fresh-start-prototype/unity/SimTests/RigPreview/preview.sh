#!/bin/sh
# Renders Nova's, Echo's or RAM's procedural rig (Rigs.cs) from six angles to <who>-preview.png, without Unity.
# Needs node, python3, the playwright package and Chromium (three.js loads from jsDelivr).
# Usage: sh preview.sh [nova|echo|ram] [out.png]
D=$(cd "$(dirname "$0")" && pwd); WHO=${1:-nova}; OUT=${2:-$D/$WHO-preview.png}; T=${TMPDIR:-/tmp}/ns-rigpreview; mkdir -p $T
python3 $D/conv.py $D/../../Assets/NovaStriker/Game/View/Rigs.cs $WHO > $T/rig.js || exit 1
{ echo '<!doctype html><html><head><meta charset="utf-8"><style>body{margin:0;background:#2b3542;position:relative}</style>'
  echo '<script type="importmap">{"imports":{"three":"https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.170.0/examples/jsm/"}}</script></head><body><script type="module">'
  cat $D/head.js $T/rig.js $D/tail.js; echo '</script></body></html>'; } > $T/preview.html
NODE_PATH=${NODE_PATH:-$(npm root -g)} node $D/shot.cjs $T/preview.html "$OUT"
