"""PreToolUse hook: ask for confirmation before any `git commit`; every other shell command passes silently."""
import json
import re
import sys

# git, optional global options (-C <path>, -c <key=value>, --no-pager, ...), then the commit subcommand
GIT_COMMIT = re.compile(r"\bgit(?:\s+(?:-[cC]\s+\S+|--?[\w-]+(?:=\S+)?))*\s+commit\b")

payload = json.load(sys.stdin)
command = payload.get("tool_input", {}).get("command", "")

if GIT_COMMIT.search(command):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "ask",
            "permissionDecisionReason": "git commit: review and approve the changes before committing.",
        }
    }))
