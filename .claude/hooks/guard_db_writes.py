"""PreToolUse hook: Claude may read the FlatMonitor database (listings.db) but never change it.

Denied, for Bash/PowerShell commands:
- SQL write statements (INSERT, UPDATE, DELETE, DROP, CREATE, ...) in a command that touches listings.db
- opening listings.db without read-only mode (sqlite URI ?mode=ro, sqlite3 -readonly, Mode=ReadOnly)
- deleting, moving, overwriting or redirecting into listings.db; `git clean -x` (deletes ignored files such as the db)
- `dotnet ef database update/drop`, unless --connection points to a database other than listings.db
- POST/PUT/PATCH/DELETE requests to a local /api/ endpoint (the running backend writes them to the db)
For Edit/Write/NotebookEdit: any file whose path contains listings.db.
Everything else passes silently. Copying FROM listings.db (e.g. into a scratch copy) stays allowed.
"""
import json
import re
import sys

DB = re.compile(r"listings\.db", re.IGNORECASE)
SQL_WRITE = re.compile(
    r"\b(INSERT|UPDATE|DELETE|REPLACE|UPSERT|DROP|CREATE|ALTER|ATTACH|DETACH|VACUUM|REINDEX|TRUNCATE|executescript)\b",
    re.IGNORECASE)
OPENS_DB = re.compile(r"sqlite3|\.connect\(|Data Source|SqliteConnection", re.IGNORECASE)
READ_ONLY = re.compile(r"mode=ro\b|-readonly\b|Mode\s*=\s*ReadOnly", re.IGNORECASE)
FILE_WRITE = re.compile(
    r"(^|[\s;&|(])(rm|del|erase|rmdir|mv|move|ren|rename|truncate|shred|Remove-Item|Move-Item|Rename-Item|"
    r"Set-Content|Add-Content|Clear-Content|Out-File|New-Item)\b[^;&|]*listings\.db",
    re.IGNORECASE)
REDIRECT_INTO_DB = re.compile(r">>?\s*['\"]?[^\s;&|'\"]*listings\.db", re.IGNORECASE)
COPY_CMD = re.compile(r"(^|[\s;&|(])(cp|copy|Copy-Item|xcopy|robocopy)\s+([^;&|]+)", re.IGNORECASE)
GIT_CLEAN_IGNORED = re.compile(r"\bgit\s+clean\b[^;&|]*\s-\w*[xX]", re.IGNORECASE)
EF_DATABASE = re.compile(r"\bdotnet\s+ef\s+database\s+(update|drop)\b", re.IGNORECASE)
EF_CONNECTION = re.compile(r"--connection\s+(\"[^\"]*\"|'[^']*'|\S+)", re.IGNORECASE)
LOCAL_API = re.compile(r"(localhost|127\.0\.0\.1)(:\d+)?/api/", re.IGNORECASE)
HTTP_CLIENT = re.compile(r"\b(curl|wget|http|Invoke-WebRequest|Invoke-RestMethod|iwr|irm)\b", re.IGNORECASE)
WRITE_METHOD = re.compile(
    r"(-X|--request|-Method)\s*['\"]?(POST|PUT|PATCH|DELETE)\b|(^|\s)(-d|--data[\w-]*|-F|--form|--json)(\s|=)",
    re.IGNORECASE)


def copies_into_db(command: str) -> bool:
    """cp/copy with listings.db as the destination (the last argument)."""
    for match in COPY_CMD.finditer(command):
        args = [a for a in re.split(r"\s+", match.group(3).strip()) if a and not a.startswith("-")]
        if args and DB.search(args[-1]):
            return True
    return False


def ef_targets_real_db(command: str) -> bool:
    if not EF_DATABASE.search(command):
        return False
    connection = EF_CONNECTION.search(command)
    # Without --connection, dotnet ef uses appsettings.json, i.e. listings.db
    return connection is None or DB.search(connection.group(1)) is not None


def reason_for_shell(command: str) -> str | None:
    touches_db = DB.search(command) is not None

    if touches_db and SQL_WRITE.search(command):
        return "SQL write statements on listings.db are not allowed; only SELECT queries."
    if touches_db and OPENS_DB.search(command) and not READ_ONLY.search(command):
        return ("Open listings.db read-only: sqlite3.connect('file:<path>/listings.db?mode=ro', uri=True), "
                "`sqlite3 -readonly`, or `Mode=ReadOnly` in the connection string.")
    if FILE_WRITE.search(command) or REDIRECT_INTO_DB.search(command) or copies_into_db(command):
        return "Deleting, moving or overwriting listings.db is not allowed (copying it elsewhere is fine)."
    if GIT_CLEAN_IGNORED.search(command):
        return "`git clean -x` would delete ignored files, including FlatMonitor/data/listings.db."
    if ef_targets_real_db(command):
        return "`dotnet ef database update/drop` would change listings.db; use --connection with a copy of the database."
    if LOCAL_API.search(command) and HTTP_CLIENT.search(command) and WRITE_METHOD.search(command):
        return "POST/PUT/PATCH/DELETE to the local API writes to listings.db; only GET requests are allowed."
    return None


def main() -> None:
    payload = json.load(sys.stdin)
    tool_input = payload.get("tool_input", {})

    if payload.get("tool_name") in ("Edit", "Write", "NotebookEdit"):
        path = tool_input.get("file_path") or tool_input.get("notebook_path") or ""
        reason = "Editing listings.db is not allowed." if DB.search(path) else None
    else:
        reason = reason_for_shell(tool_input.get("command", ""))

    if reason:
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": f"Database is read-only for Claude: {reason}",
            }
        }))


if __name__ == "__main__":
    main()
