#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
: "${CXX:=x86_64-w64-mingw32-clang++}"
mkdir -p bin
"$CXX" -std=c++17 -O2 -Wall -Wextra -Werror -Wno-unknown-pragmas -shared -static \
  -o bin/NovrFoveation.dll NovrFoveation.cpp -ld3d11 -ldxgi -lole32 \
  -Wl,--no-insert-timestamp -Wl,--dynamicbase -Wl,--nxcompat
