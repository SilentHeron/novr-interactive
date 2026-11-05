#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
check_binary="$(mktemp "${TMPDIR:-/tmp}/novr-ffr-pattern-check.XXXXXX")"
trap 'rm -f "$check_binary"' EXIT
for test_source in PatternChecks.cpp RateMapChecks.cpp QueryChecks.cpp ShadingRatesChecks.cpp; do
  "${HOST_CXX:-c++}" -std=c++17 -O2 -Wall -Wextra -Werror "$test_source" -o "$check_binary"
  "$check_binary"
done
