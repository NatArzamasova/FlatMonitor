#!/bin/bash
# Setup git hooks for this project

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
GIT_HOOKS_DIR="$REPO_ROOT/.git/hooks"

echo "Setting up git hooks..."

# Copy commit-msg hook
if [ -f "$SCRIPT_DIR/hooks/commit-msg" ]; then
    cp "$SCRIPT_DIR/hooks/commit-msg" "$GIT_HOOKS_DIR/commit-msg"
    chmod +x "$GIT_HOOKS_DIR/commit-msg"
    echo "✅ Installed commit-msg hook"
else
    echo "❌ Hook file not found: $SCRIPT_DIR/hooks/commit-msg"
    exit 1
fi

echo "✅ All hooks installed successfully"
