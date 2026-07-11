#!/bin/sh
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
compiler=${CC:-clang}
output=${1:-"$root/artifacts/ffdecsa-benchmark"}

mkdir -p "$(dirname -- "$output")"
"$compiler" -O3 -DPARALLEL_MODE=1283 -I"$root/references/FFdecsa" \
  "$root/tools/ffdecsa-compare/ffdecsa_benchmark.c" \
  "$root/references/FFdecsa/FFdecsa.c" \
  -o "$output"
"$output"
