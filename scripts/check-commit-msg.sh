#!/bin/sh
# Validate commit messages against: type(scope): message
# Types: feat, fix, refactor, docs, test, chore
#
# Usage:
#   check-commit-msg.sh <message-file>        checks the first line (used by the commit-msg hook)
#   check-commit-msg.sh --range <from>..<to>  checks every non-merge commit in the range (used by CI)
#
# POSIX sh on purpose: CI runs it in a minimal Alpine image without bash.

PATTERN='^(feat|fix|refactor|docs|test|chore)\([a-z0-9_-]+\): .+'

print_help() {
  echo ""
  echo "Valid types: feat, fix, refactor, docs, test, chore"
  echo "Example: feat(scrape): add HTTP client for pagination"
}

if [ "${1:-}" = "--range" ]; then
  range="${2:?usage: check-commit-msg.sh --range <from>..<to>}"
  from="${range%%..*}"
  to="${range##*..}"

  # Force push or shallow history: the old tip may not exist here. Check the pushed tip only.
  if ! git cat-file -e "${from}^{commit}" 2>/dev/null; then
    echo "Commit $from is not available; checking only $to."
    range="${to}~1..${to}"
    if ! git cat-file -e "${to}~1^{commit}" 2>/dev/null; then
      range="$to"   # root commit
    fi
  fi

  bad=$(git log --no-merges --format='%h %s' "$range" | grep -vE "^[0-9a-f]+ ${PATTERN#^}" || true)
  checked=$(git log --no-merges --format='%h' "$range" | wc -l | tr -d ' ')

  if [ -n "$bad" ]; then
    echo "❌ These commit messages do not match the required format: type(scope): message"
    echo "$bad"
    print_help
    exit 1
  fi
  echo "✅ $checked commit message(s) OK"
  exit 0
fi

message_file="${1:?usage: check-commit-msg.sh <message-file> | --range <from>..<to>}"
first_line=$(head -n1 "$message_file")

if echo "$first_line" | grep -qE "$PATTERN"; then
  exit 0
fi

echo "❌ Commit message does not match required format: type(scope): message"
print_help
echo ""
echo "Your message: $first_line"
exit 1
