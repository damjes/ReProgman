#!/usr/bin/env sh
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
rid=${1:-linux-x64}
configuration=${CONFIGURATION:-Release}
case "$rid" in
    linux-x64|linux-arm64) ;;
    *) printf 'Supported runtimes: linux-x64, linux-arm64\n' >&2; exit 2 ;;
esac

output="$root/artifacts/$rid"
staging=$(mktemp -d)
trap 'rm -rf "$staging"' EXIT HUP INT TERM

dotnet publish "$root/src/ReProgman" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    --output "$staging"

mkdir -p "$output"
rm -rf "$output/ReProgman"
mkdir -p "$output/ReProgman"
cp -R "$staging"/. "$output/ReProgman/"
chmod +x "$output/ReProgman/ReProgman"
tar -C "$output" -czf "$output/ReProgman-$rid.tar.gz" ReProgman
printf 'Published %s\n' "$output/ReProgman"
printf 'Archive: %s\n' "$output/ReProgman-$rid.tar.gz"
