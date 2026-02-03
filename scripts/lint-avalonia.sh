#!/usr/bin/env bash
set -euo pipefail

fail=0

check_pattern() {
  local pattern="$1"
  shift
  if grep -R -n --exclude-dir .git --exclude-dir bin --exclude-dir obj "$@" "$pattern" .; then
    echo "[lint-avalonia] Forbidden token found: $pattern" >&2
    fail=1
  fi
}

check_pattern "SetterTargetType" --include="*.axaml" --include="*.xaml"
check_pattern "ElementName='\"" --include="*.axaml" --include="*.xaml"
check_pattern " Mode=" --include="*.axaml" --include="*.xaml"
check_pattern "WrapPanel.*Spacing" --include="*.axaml" --include="*.xaml"
check_pattern "TargetFrameworkAttribute" --include="*.cs" --include="*.csproj"

if [[ $fail -ne 0 ]]; then
  echo "[lint-avalonia] Lint failed." >&2
  exit 1
fi

echo "[lint-avalonia] OK."
