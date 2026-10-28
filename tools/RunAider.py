#!/usr/bin/env python3
"""Run one bounded Tongji Aider edit: python3 tools/RunAider.py 'task' file ..."""

import argparse
import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import tempfile


REPO = Path(__file__).resolve().parents[1]
MODEL = "openai/DeepSeek-V4.1-Flash"
HOST = "agi.tongji.edu.cn"


def read_key():
    # Read this key only; never execute the env file or print its contents.
    for line in (Path.home() / ".codex/.env").read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line.startswith("export "):
            line = line[7:].lstrip()
        name, separator, value = line.partition("=")
        if separator and name.strip() == "TONGJI_API_KEY":
            parts = shlex.split(value, comments=True)
            if len(parts) == 1 and parts[0]:
                return parts[0]
            raise ValueError("TONGJI_API_KEY is empty or malformed")
    raise ValueError("TONGJI_API_KEY is missing from ~/.codex/.env")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout", type=int, default=180, help="Total seconds; default 180")
    parser.add_argument("--max-tokens", type=int, default=8192, help="Reply token limit; default 8192, maximum 32768")
    parser.add_argument("task", help="One concrete edit and its acceptance conditions")
    parser.add_argument("files", nargs="+", help="Only the required repository files")
    args = parser.parse_args()
    if not args.task.strip() or args.timeout <= 0 or not 1 <= args.max_tokens <= 32768:
        parser.error("Supply a task, positive timeout, and 1..32768 reply tokens")
    files = []
    for supplied in args.files:
        path = (REPO / supplied).resolve()
        relative = path.relative_to(REPO)
        if not path.is_file() or any(part.startswith(".") for part in relative.parts):
            parser.error("Use existing, non-hidden files inside this repository")
        if relative.as_posix() not in files:
            files.append(relative.as_posix())
    executable = shutil.which("aider") or str(Path.home() / ".local/bin/aider")
    if not Path(executable).is_file():
        parser.error("Aider is not installed or executable is unavailable")
    key = read_key()
    # Changes apply only to the child process, preserving the system/Codex proxy.
    env = {name: value for name, value in os.environ.items() if not name.startswith("AIDER_")}
    env.update(OPENAI_API_KEY=key, OPENAI_API_BASE="https://agi.tongji.edu.cn/v1")
    for name in ("NO_PROXY", "no_proxy"):
        values = [item.strip() for item in env.get(name, "").split(",") if item.strip()]
        env[name] = ",".join(dict.fromkeys([*values, HOST]))
    message = (
        "Edit only these supplied files. Preserve existing uncommitted changes. "
        "Do not add other files, run commands, commit, deploy, or conduct independent review, "
        "philosophical research or multi-model discussion. Apply the requested edit, "
        "then give at most three short result lines. Task: " + args.task
    )
    with tempfile.TemporaryDirectory(prefix="sts2-aider-") as temporary:
        config = Path(temporary) / "config.yml"
        config.write_text("{}\n", encoding="utf-8")
        settings = Path(temporary) / "models.json"
        settings.write_text(json.dumps([{
            "name": MODEL, "edit_format": "diff", "weak_model_name": MODEL,
            "extra_params": {
                "max_tokens": args.max_tokens, "num_retries": 0,
                "extra_body": {"chat_template_kwargs": {"thinking": False}},
            },
        }]), encoding="utf-8")
        command = [
            executable, "--model", MODEL, "--model-settings-file", str(settings),
            "--config", str(config), "--env-file", os.devnull,
            "--map-tokens", "0", "--no-stream", "--no-auto-commits", "--no-dirty-commits",
            "--no-gitignore", "--no-add-gitignore-files", "--no-restore-chat-history",
            "--input-history-file", os.devnull, "--chat-history-file", os.devnull,
            "--max-chat-history-tokens", "32768",
            "--no-auto-lint", "--no-auto-test", "--no-suggest-shell-commands",
            "--no-detect-urls", "--no-analytics", "--no-check-update", "--no-show-release-notes",
            "--no-show-model-warnings",
            "--no-pretty", "--yes-always", "--timeout", "180", "--message", message, *files,
        ]
        # No wrapper retries. Aider's own bounded reflection/error handling is
        # additionally capped by this deadline; edits made before timeout remain.
        with tempfile.TemporaryFile(mode="w+b") as output:
            try:
                result = subprocess.run(command, cwd=REPO, env=env, stdin=subprocess.DEVNULL,
                                        stdout=output, stderr=subprocess.STDOUT, timeout=args.timeout)
                code = result.returncode
            except subprocess.TimeoutExpired:
                code = 124
                print("Aider deadline reached; inspect the preserved diff before any retry.")
            output.seek(0, os.SEEK_END)
            output.seek(max(0, output.tell() - 6000))
            print(output.read().decode("utf-8", errors="replace").replace(key, "[REDACTED]"))
        print("Aider exit:", code, "| files:", ", ".join(files))
        return code


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        raise SystemExit(str(error))
