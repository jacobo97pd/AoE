#!/usr/bin/env python3
"""Prepare a Windows-exported Unity Xcode project on a Codemagic Mac."""
from __future__ import annotations

import json
import os
from pathlib import Path
import plistlib
import re
import stat
import sys

MACHO_MAGIC = {bytes.fromhex(value) for value in (
    "feedface", "cefaedfe", "feedfacf", "cffaedfe", "cafebabe", "bebafeca", "cafebabf", "bfbafeca"
)}


def prepare(root: Path, environment: dict[str, str]) -> dict:
    ios = root / "ios"
    project = ios / "Unity-iPhone.xcodeproj/project.pbxproj"
    plist_path = ios / "Info.plist"
    if not project.is_file() or not plist_path.is_file():
        raise ValueError("Missing Unity Xcode export. Keep ios/ next to codemagic.yaml.")
    bundle_id = environment.get("BUNDLE_ID", "com.emberfield.prototype")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9-]*(?:\.[A-Za-z0-9][A-Za-z0-9-]*)+", bundle_id):
        raise ValueError("BUNDLE_ID must be an Apple bundle identifier, without wildcards.")
    # Codemagic's per-application build number starts at zero; Apple requires a positive value.
    build_number = int(environment.get("BUILD_NUMBER", "0")) + int(environment.get("BUILD_NUMBER_OFFSET", "0")) + 1
    if build_number < 1:
        raise ValueError("The resulting build number must be positive.")
    plist = plistlib.loads(plist_path.read_bytes())
    old_id = plist.get("CFBundleIdentifier", "")
    text = project.read_text(encoding="utf-8")
    candidates = {"com.emberfield.prototype"}
    if old_id and "$" not in old_id:
        candidates.add(old_id)
    summary_path = ios / "emberfield-export-summary.txt"
    if summary_path.is_file():
        exported = re.search(r"^Bundle: ([A-Za-z0-9.-]+)$", summary_path.read_text(encoding="utf-8"), re.MULTILINE)
        if exported:
            candidates.add(exported[1])
    changed = 0

    def bundle(match: re.Match) -> str:
        nonlocal changed
        if match[2].strip().strip('"') not in candidates:
            return match[0]
        changed += 1
        return match[1] + '"' + bundle_id + '";'

    text = re.sub(r'(PRODUCT_BUNDLE_IDENTIFIER\s*=\s*)([^;\n]+);', bundle, text)
    if changed == 0:
        raise ValueError("Could not identify the application's bundle ID in project.pbxproj; refusing to edit framework IDs.")
    text = re.sub(r'(CURRENT_PROJECT_VERSION\s*=\s*)[^;\n]+;', lambda m: m[1] + str(build_number) + ';', text)
    # Fail on unresolved Git LFS pointers before Xcode emits misleading native errors.
    executable_count = 0
    for path in sorted(ios.rglob("*")):
        if not path.is_file():
            continue
        if not path.resolve().is_relative_to(ios.resolve()):
            raise ValueError("An exported file links outside ios/.")
        with path.open("rb") as stream:
            header = stream.read(128)
        if header.startswith(b"version https://git-lfs.github.com/spec/v1"):
            raise ValueError("Unresolved Git LFS file: " + str(path.relative_to(root)))
        if header.startswith(b"#!") or path.suffix == ".sh":
            script = path.read_bytes()
            if b"\r\n" in script:
                path.write_bytes(script.replace(b"\r\n", b"\n"))
        # Windows ZIP extraction does not preserve executable permission bits.
        if header[:4] in MACHO_MAGIC or header.startswith(b"#!") or path.suffix == ".sh":
            path.chmod(path.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
            executable_count += 1
    plist["CFBundleIdentifier"] = bundle_id
    plist["CFBundleVersion"] = str(build_number)
    plist_path.write_bytes(plistlib.dumps(plist, sort_keys=False))
    project.write_text(text, encoding="utf-8", newline="\n")
    report = {
        "bundle_id": bundle_id,
        "version": plist.get("CFBundleShortVersionString"),
        "build_number": build_number,
        "deployment_target": plist.get("MinimumOSVersion"),
        "executable_permissions_restored": executable_count,
        "privacy_manifests": [str(p.relative_to(ios)) for p in ios.rglob("PrivacyInfo.xcprivacy")],
        "status": "Export prepared; compilation and signing are separate steps.",
    }
    (root / "build").mkdir(exist_ok=True)
    (root / "build/package-check.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    try:
        print(json.dumps(prepare(Path(__file__).resolve().parents[1], dict(os.environ)), indent=2))
    except (ValueError, OSError) as error:
        sys.exit(str(error))
