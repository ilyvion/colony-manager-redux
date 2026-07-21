#!/usr/bin/env python3
"""Update filename header and copyright year in *.cs files based on git history."""
import os
import re
import subprocess
import sys
from datetime import date

REPO_ROOT = subprocess.run(
    ["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, check=True
).stdout.strip()

FILENAME_RE = re.compile(r"^// (.+\.cs)$")
COPYRIGHT_RE = re.compile(
    r"^(// Copyright \(c\) )(\d{4})(?:[–-](\d{4}))?( Alexander Krivács Schrøder)$"
)
AUTHOR = "Alexander Krivács Schrøder <alexschrod@gmail.com>"

DRY_RUN = "--apply" not in sys.argv
UPDATE_MISSING = "--update-missing" in sys.argv


def git_last_modified_year(path: str) -> int:
    result = subprocess.run(
        ["git", "log", "-1", "--format=%cd", "--date=format:%Y", "--", path],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=True,
    )
    year = result.stdout.strip()
    if year:
        return int(year)
    # Untracked/new file: fall back to today's year.
    return date.today().year


def git_author_first_year(path: str) -> int:
    """Year of the earliest commit touching this file authored by AUTHOR."""
    result = subprocess.run(
        [
            "git", "log", "--follow", f"--author={AUTHOR}",
            "--format=%cd", "--date=format:%Y", "--reverse", "--", path,
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=True,
    )
    years = result.stdout.strip().splitlines()
    if years:
        return int(years[0])
    # Fallback: no commit by that author touched this file (e.g. untracked, or
    # entirely written by someone else) - use the earliest commit of any author.
    result = subprocess.run(
        ["git", "log", "--follow", "--format=%cd", "--date=format:%Y", "--reverse", "--", path],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=True,
    )
    years = result.stdout.strip().splitlines()
    if years:
        return int(years[0])
    return date.today().year


def find_cs_files():
    for dirpath, dirnames, filenames in os.walk(REPO_ROOT):
        dirnames[:] = [d for d in dirnames if d not in ("obj", "bin", ".git")]
        for name in filenames:
            if name.endswith(".cs"):
                yield os.path.join(dirpath, name)


def process_file(path: str) -> tuple[list[str], bool, bool]:
    """Returns (change descriptions, had_filename_header, had_copyright_line)."""
    rel = os.path.relpath(path, REPO_ROOT)
    changes = []

    with open(path, "r", encoding="utf-8-sig", newline="") as f:
        content = f.read()
    has_bom = False
    with open(path, "rb") as f:
        has_bom = f.read(3) == b"\xef\xbb\xbf"

    lines = content.splitlines(keepends=True)
    if not lines:
        return [], False, False

    expected_name = os.path.basename(path)
    modified = False

    had_filename_header = bool(FILENAME_RE.match(lines[0].rstrip("\r\n")))
    m = FILENAME_RE.match(lines[0].rstrip("\r\n"))
    if m:
        actual_name = m.group(1)
        if actual_name != expected_name:
            eol = lines[0][len(lines[0].rstrip("\r\n")):]
            lines[0] = f"// {expected_name}{eol}"
            changes.append(f"filename: {actual_name} -> {expected_name}")
            modified = True

    had_copyright_line = False
    idx = 1
    while idx < len(lines) and lines[idx].rstrip("\r\n").startswith("//"):
        stripped = lines[idx].rstrip("\r\n")
        m = COPYRIGHT_RE.match(stripped)
        if m:
            had_copyright_line = True
            prefix, start_year, end_year, suffix = m.groups()
            mod_year = git_last_modified_year(path)
            new_end = str(mod_year)
            if int(start_year) == mod_year:
                # Already up to date as a single year; leave as-is.
                break
            if new_end == start_year:
                break
            if end_year == new_end:
                break
            eol = lines[idx][len(stripped):]
            lines[idx] = f"{prefix}{start_year}–{new_end}{suffix}{eol}"
            old_display = f"{start_year}" + (f"–{end_year}" if end_year else "")
            changes.append(f"copyright: {old_display} -> {start_year}–{new_end}")
            modified = True
            break
        idx += 1

    if UPDATE_MISSING and not had_copyright_line:
        eol = lines[0][len(lines[0].rstrip("\r\n")):] or "\n"
        start_year = git_author_first_year(path)
        end_year = git_last_modified_year(path)
        year_str = str(start_year) if start_year == end_year else f"{start_year}–{end_year}"
        copyright_line = f"// Copyright (c) {year_str} Alexander Krivács Schrøder{eol}"

        if not had_filename_header:
            filename_line = f"// {expected_name}{eol}"
            lines = [filename_line, copyright_line, eol] + lines
            changes.append(f"added header: filename + copyright ({year_str})")
        else:
            insert_at = 1
            while insert_at < len(lines) and lines[insert_at].rstrip("\r\n").startswith("//"):
                insert_at += 1
            lines.insert(insert_at, copyright_line)
            changes.append(f"added copyright line ({year_str})")
        modified = True

    if modified and not DRY_RUN:
        new_content = "".join(lines)
        with open(path, "w", encoding="utf-8-sig" if has_bom else "utf-8", newline="") as f:
            f.write(new_content)

    return [f"{rel}: {c}" for c in changes], had_filename_header, had_copyright_line


def main():
    all_changes = []
    no_filename_header = []
    no_copyright_line = []
    for path in sorted(find_cs_files()):
        rel = os.path.relpath(path, REPO_ROOT)
        changes, had_filename_header, had_copyright_line = process_file(path)
        all_changes.extend(changes)
        if not had_filename_header:
            no_filename_header.append(rel)
        if not had_copyright_line:
            no_copyright_line.append(rel)

    for line in all_changes:
        print(line)
    print(f"\n{len(all_changes)} change(s) across files. Mode: {'DRY RUN' if DRY_RUN else 'APPLIED'}")

    if no_filename_header:
        print(f"\n{len(no_filename_header)} file(s) with no `// <name>.cs` line 1 (unaffected, needs manual review):")
        for rel in no_filename_header:
            print(f"  {rel}")

    if no_copyright_line:
        print(f"\n{len(no_copyright_line)} file(s) with no Schrøder copyright line on line 2/3 (unaffected, needs manual review):")
        for rel in no_copyright_line:
            print(f"  {rel}")


if __name__ == "__main__":
    main()
