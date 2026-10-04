#!/usr/bin/env bash
# Runs the Godot port's test suites headless and fails on any failed check or any script error (GDScript has no
# exceptions: a runtime error is printed and the function returns, so the output is checked for it too).
# Usage: tools/run-tests.sh [filter]   (filter: only suites whose file name contains it)
# GODOT: the Godot 4.3 binary (default: godot on the PATH).
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
godot="${GODOT:-godot}"
arg="--tests"
[ $# -gt 0 ] && arg="--tests=$1"
"$godot" --headless --path "$here/../project" --import >/dev/null 2>&1
out="$("$godot" --headless --path "$here/../project" -- "$arg" 2>&1)"
status=$?
echo "$out" | grep -vE 'resources still in use at exit|^ +at: (cleanup|clear) \(core/'
if echo "$out" | grep -qE '^(SCRIPT ERROR|ERROR: .*res://)'; then
  echo "FAIL: script errors in the run (see above)"
  exit 1
fi
exit $status
