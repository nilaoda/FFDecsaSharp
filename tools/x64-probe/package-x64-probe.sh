#!/bin/sh
set -eu

rid=${1:-win-x64}
output_directory=${2:-"$HOME/Downloads"}
root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
timestamp=$(date +%Y%m%d-%H%M%S)
publish_directory="$root/artifacts/perf-probe-$rid-$timestamp"
archive="$output_directory/FFDecsaSharp-x64-probe-$rid-$timestamp.zip"

mkdir -p "$output_directory" "$publish_directory"
dotnet publish -c Release "$root/src/FFDecsaSharp.PerfHarness/FFDecsaSharp.PerfHarness.csproj" \
  -r "$rid" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishAot=false \
  -p:PublishTrimmed=false \
  -o "$publish_directory"
cp "$root/docs/X86_PROBE.md" "$publish_directory/README-X86-PROBE.md"
if [ "$rid" = "win-x64" ]; then
  cp "$root/tools/x64-probe/Run-X64-Probe.cmd" "$publish_directory/Run-X64-Probe.cmd"
  cp "$root/tools/x64-probe/Run-CSharp-Reference.cmd" "$publish_directory/Run-CSharp-Reference.cmd"
  cp "$root/tools/x64-probe/Run-Jit-Disasm.cmd" "$publish_directory/Run-Jit-Disasm.cmd"
  cp "$root/tools/x64-probe/Run-C-Reference.cmd" "$publish_directory/Run-C-Reference.cmd"
  cp "$root/tools/x64-probe/Run-All-Benchmarks.cmd" "$publish_directory/Run-All-Benchmarks.cmd"
  mkdir -p "$publish_directory/c-reference"
  cp "$root/tools/ffdecsa-compare/ffdecsa_benchmark.c" "$publish_directory/c-reference/ffdecsa_benchmark.c"
  cp "$root/tools/x64-probe/ffdecsa_msvc_compat.h" "$publish_directory/c-reference/ffdecsa_msvc_compat.h"
  cp -R "$root/references/FFdecsa" "$publish_directory/ffdecsa-reference"
fi
(cd "$publish_directory" && zip -q -r "$archive" . -x '*.dbg' -x '*.pdb' -x '*.xml')
printf '%s\n' "$archive"
