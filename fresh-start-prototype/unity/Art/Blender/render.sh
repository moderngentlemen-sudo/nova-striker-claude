#!/bin/sh
# Builds a character and renders its review sheet. Usage: sh render.sh <build_script.py> <out-dir> <name> [samples] [helmet]
B=${BLENDER:-blender}; D=$(cd "$(dirname "$0")" && pwd)
$B -b -P $D/$1 -- $2 2>&1 | grep -iE 'Traceback|Error|built' || exit 1
BLEND=$2/$(basename $1 .py | sed 's/build_//').blend
$B -b $BLEND -P $D/render_turnaround.py -- $2/$3.png ${4:-32} $5 2>&1 | grep -iE 'Traceback|Error' 
python3 - "$2/$3" <<'PY'
import sys
from PIL import Image
b = sys.argv[1]; ims = [Image.open(f'{b}_{n}.png') for n in ('front', 'three_quarter', 'side', 'back', 'face')]
s = Image.new('RGB', (sum(i.width for i in ims), max(i.height for i in ims))); x = 0
for i in ims: s.paste(i, (x, 0)); x += i.width
s.save(f'{b}_sheet.png')
PY
