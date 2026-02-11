#!/usr/bin/env bash
set -euo pipefail

fail=0

check_pattern() {
  local pattern="$1"
  shift
  if rg -n "$pattern" "$@" .; then
    echo "[lint-avalonia] Forbidden token found: $pattern" >&2
    fail=1
  fi
}

check_pattern "SetterTargetType" -g '*.axaml' -g '*.xaml'
check_pattern "ElementName='\\\"" -g '*.axaml' -g '*.xaml'
check_pattern "<FluentTheme[^>]*\\sMode=|<StyleInclude[^>]*\\sMode=" -g '*.axaml' -g '*.xaml'
check_pattern "WrapPanel[^>]*Spacing=" -g '*.axaml' -g '*.xaml'
check_pattern "TargetFrameworkAttribute" -g '*.cs'

if [[ $fail -ne 0 ]]; then
  echo "[lint-avalonia] Lint failed." >&2
  exit 1
fi

echo "[lint-avalonia] OK."
