#!/usr/bin/env bash
set -euo pipefail
src="${1:-examples/ExampleLevel}"
out="${2:-ExampleLevel.brlevel.zip}"
(cd "$src" && zip -r "../../$out" .)
echo "Packed: $out"
