#!/bin/sh
# Every zone, every character, alone and with three AI teammates: the C# port against the JavaScript prototype.
# Usage: sh parity.sh [ticks] [seeds]
TICKS=${1:-1500}; SEEDS=${2:-"1 2"}; VARIANTS=${3:-""}
DOTNET=${DOTNET:-$HOME/.dotnet/dotnet}
OUT=${TMPDIR:-/tmp}/ns-parity; mkdir -p $OUT
$DOTNET build -nologo -v q >/dev/null || exit 1
fail=0; pass=0
for zone in gym arena tower skyline foundry undercity warden beacon; do
  for who in nova echo ram fix; do
    for bots in 0 3; do
     for v in "" $VARIANTS; do
      for seed in $SEEDS; do
        s=$zone-$who-$bots${v:+-$v}
        node trace.mjs $s $TICKS $seed > $OUT/js.txt 2>$OUT/js.err
        $DOTNET bin/Debug/net8.0/SimTests.dll $s $TICKS $seed > $OUT/cs.txt 2>$OUT/cs.err
        r=$(python3 compare.py $OUT/js.txt $OUT/cs.txt)
        case "$r" in MATCH*) pass=$((pass+1));; *) fail=$((fail+1)); echo "$s seed $seed: $r"; head -c 300 $OUT/cs.err;; esac
      done
     done
    done
  done
done
echo "$pass match, $fail differ"
