#!/usr/bin/env python3
"""Create a portable Unity source snapshot or a verified Codemagic Xcode ZIP.

No Unity, downloads, signing, credentials or network services are invoked. Run
``prepare --destination ABSOLUTE_NEW_DIRECTORY`` before the separate Unity iOS
export, then ``package --export XCODE_DIRECTORY --support SUPPORT_DIRECTORY
--output NEW_ZIP``. Inputs are never modified.
"""

from __future__ import annotations

import argparse
import base64
import binascii
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import unicodedata
import zipfile


class PackageError(Exception):
    """A validation failure whose message contains no credential values."""


SOURCE_MANIFEST = "portable-source-manifest.json"
SOURCE_MARKER = ".emberfield-ios-package"
ZIP_MANIFEST = "package-manifest.json"
QUALITY = "ProjectSettings/QualitySettings.asset"
IOS_OVERLAY = (
    "Assets/Game/Editor/IosBuildTools.cs",
    "Assets/Game/Editor/IosBuildTools.cs.meta",
    "Assets/Tests/Editor.meta",
    "Assets/Tests/Editor/Emberfield.Tests.Editor.asmdef",
    "Assets/Tests/Editor/Emberfield.Tests.Editor.asmdef.meta",
    "Assets/Tests/Editor/IosBuildToolsTests.cs",
    "Assets/Tests/Editor/IosBuildToolsTests.cs.meta",
)
# These are the source project's existing, intentional relocations. A nested
# link may stay inside its corresponding root, but may not escape it.
ASSET_ROOTS = tuple("Assets/Game/" + name for name in (
    "ArtOverhaul", "ArtStyleLab", "CrimsonCorsair", "KingdomPremium",
    "PirateCrew", "ReferenceCharacters", "Resources", "RoyalSoldier", "Scenes",
))
CHUNK = 1024 * 1024
SENSITIVE_SUFFIXES = {".p8", ".p12", ".pfx", ".mobileprovision", ".keystore", ".jks", ".ulf"}
PRIVATE_KEY = re.compile(rb"-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED )?PRIVATE KEY-----")
PROVIDER_TOKEN = re.compile(
    rb"(?<![A-Za-z0-9])(?:"
    rb"(?:sk-(?:proj-|svcacct-|ant-)?)[A-Za-z0-9_-]{24,}|"
    rb"(?:AKIA|ASIA)[A-Z0-9]{16}|"
    rb"gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|"
    rb"xox[baprs]-[A-Za-z0-9-]{20,}|AIza[A-Za-z0-9_-]{30,}"
    rb")(?![A-Za-z0-9])"
)
# Only quoted literal assignments, or bare config lines, qualify. A source
# expression such as password = field.text, and ${ENVIRONMENT_REFERENCES}, do not.
SECRET_NAME = rb"(?:[A-Za-z][A-Za-z0-9]*[_-])*(?:api[_-]?key|api[_-]?secret|client[_-]?secret|secret[_-]?access[_-]?key|access[_-]?token|refresh[_-]?token|token|password|private[_-]?key|license|serial)"
SECRET_LITERAL = re.compile(
    rb"(?im)(?:[\"']?\b" + SECRET_NAME + rb"[\"']?[ \t]*[:=][ \t]*[\"']([A-Za-z0-9_+/.=-]{12,})[\"']|"
    rb"^[ \t]*" + SECRET_NAME + rb"[ \t]*:[ \t]*([A-Za-z0-9_+/.=-]{12,})[ \t]*$)"
)
PLACEHOLDER = re.compile(rb"(?i)^(?:placeholder|changeme|replace[_-].*|your[_-].*|example[_-].*|test[_-].*|dummy[_-].*|[xX*]+)$")


def inside(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def relative_name(name: str) -> str:
    path = PurePosixPath(name)
    if (not name or "\\" in name or ":" in name or path.is_absolute()
            or any(part in ("", ".", "..") for part in name.split("/"))):
        raise PackageError("Unsafe relative path: " + name)
    return path.as_posix()


def reject_sensitive_name(name: str) -> None:
    for part in PurePosixPath(name).parts:
        lower = part.lower()
        if (lower == ".env" or lower.startswith(".env.")
                or Path(lower).suffix in SENSITIVE_SUFFIXES
                or lower in {"id_rsa", "id_dsa", "id_ecdsa", "id_ed25519"}):
            raise PackageError("Sensitive file is not permitted: " + name)


def reject_sensitive_content(data: bytes, name: str) -> None:
    # Crypto libraries legitimately contain PEM delimiter constants. Reject a
    # complete key block with a decodable payload, not the delimiter alone.
    for match in PRIVATE_KEY.finditer(data):
        end = match.group().replace(b"BEGIN", b"END", 1)
        end_index = data.find(end, match.end(), match.end() + 65536)
        if end_index < 0:
            continue
        body = data[match.end():end_index].replace(b"\\r", b"").replace(b"\\n", b"\n")
        lines = [line.strip(b" \t\r\"';,") for line in body.splitlines()]
        payload = b"".join(line for line in lines if re.fullmatch(rb"[A-Za-z0-9+/=]{16,}", line))
        if len(payload) >= 64:
            try:
                decoded = base64.b64decode(payload, validate=True)
            except binascii.Error:
                continue
            if len(decoded) >= 32:
                raise PackageError("Private key detected in " + name)
    if PROVIDER_TOKEN.search(data):
        raise PackageError("Provider credential detected in " + name)
    literal_data = data
    if name.endswith("/machine.config") and "/mono/" in name:
        # Mono's strong-name map pairs a 16-hex PUBLIC key token with a public
        # RSA key. These SDK identifiers are not authentication credentials.
        # Exclude only these complete mapping elements, never the whole config.
        literal_data = re.sub(rb'<map\s+Token="[0-9a-fA-F]{16}"\s+PublicKey="[0-9a-fA-F]{64,}"\s*/>', b'', data)
    for match in SECRET_LITERAL.finditer(literal_data):
        value = match.group(1) or match.group(2)
        if not PLACEHOLDER.fullmatch(value):
            raise PackageError("Literal credential assignment detected in " + name)


def collision_check(names: list[str]) -> None:
    """Check even on a case-sensitive runner: macOS users commonly use APFS case-insensitive."""
    seen: dict[str, str] = {}
    parents: dict[str, str] = {}
    for name in names:
        relative_name(name)
        key = unicodedata.normalize("NFC", name).casefold()
        if key in seen:
            raise PackageError("Portable path collision: " + seen[key] + " and " + name)
        seen[key] = name
        for parent in PurePosixPath(name).parents:
            original = parent.as_posix()
            parent_key = unicodedata.normalize("NFC", original).casefold()
            if parent_key in parents and parents[parent_key] != original:
                raise PackageError("Portable directory collision: " + parents[parent_key] + " and " + original)
            parents[parent_key] = original
    for key, name in seen.items():
        for parent in PurePosixPath(key).parents:
            if parent.as_posix() in seen:
                raise PackageError("File/directory collision: " + name)


def git(repo: Path, *arguments: str) -> bytes:
    result = subprocess.run(["git", "-C", str(repo), *arguments], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        # stderr can contain remote URLs or user configuration. Do not echo it.
        raise PackageError("Git could not read the source repository (" + arguments[0] + ").")
    return result.stdout


def source_allowed(name: str) -> bool:
    return (name.startswith("Assets/") or name.startswith("ProjectSettings/")
            or name in {"Packages/manifest.json", "Packages/packages-lock.json"})


def source_file(repo: Path, name: str) -> Path:
    logical = repo / relative_name(name)
    allowed = repo
    for prefix in ASSET_ROOTS:
        if name.startswith(prefix + "/"):
            allowed = (repo / prefix).resolve(strict=True)
            break
    try:
        resolved = logical.resolve(strict=True)
    except (OSError, RuntimeError) as error:
        raise PackageError("Missing or invalid source file: " + name) from error
    if not inside(resolved, allowed) or not resolved.is_file():
        raise PackageError("Source link escapes its approved root or is not a file: " + name)
    return resolved


def transfer(source: Path, sink, name: str) -> tuple[int, str]:
    """Stream a regular file, checking secrets, SHA-256 and concurrent changes."""
    reject_sensitive_name(name)
    before = source.stat()
    if not stat.S_ISREG(before.st_mode):
        raise PackageError("Only regular files are accepted: " + name)
    digest = hashlib.sha256()
    size = 0
    tail = b""
    with source.open("rb") as stream:
        while True:
            block = stream.read(CHUNK)
            if not block:
                break
            if size == 0 and block.startswith(b"version https://git-lfs.github.com/spec/v1"):
                raise PackageError("Unmaterialized Git LFS pointer: " + name)
            reject_sensitive_content(tail + block, name)
            tail = (tail + block)[-65536:]
            digest.update(block)
            size += len(block)
            if sink is not None:
                sink.write(block)
    after = source.stat()
    if (before.st_size, before.st_mtime_ns, before.st_ino) != (after.st_size, after.st_mtime_ns, after.st_ino) or size != before.st_size:
        raise PackageError("Input changed during packaging: " + name)
    return size, digest.hexdigest()


def json_bytes(document: dict) -> bytes:
    return (json.dumps(document, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def prepare(repo: Path, destination: Path) -> dict:
    repo = repo.resolve(strict=True)
    if not destination.is_absolute():
        raise PackageError("--destination must be an absolute path.")
    if destination.is_symlink() or getattr(destination, "is_junction", lambda: False)():
        raise PackageError("--destination must not be a link.")
    destination = destination.resolve()
    if destination.exists() and (not destination.is_dir() or any(destination.iterdir())):
        raise PackageError("--destination already exists and is not empty.")
    if inside(repo, destination) or any(inside(destination, repo / part) for part in ("Assets", "Packages", "ProjectSettings", ".git")):
        raise PackageError("--destination overlaps source project files.")
    commit = git(repo, "rev-parse", "HEAD").decode("ascii").strip()
    tracked = {name for name in git(repo, "ls-files", "-z").decode("utf-8").split("\0") if name}
    names = sorted({name for name in tracked if source_allowed(name)} | set(IOS_OVERLAY))
    collision_check(names + [SOURCE_MANIFEST, SOURCE_MARKER])
    for required in (*IOS_OVERLAY, QUALITY, "ProjectSettings/ProjectVersion.txt", "Packages/manifest.json", "Packages/packages-lock.json"):
        if required not in names or not (repo / required).is_file():
            raise PackageError("Required source is missing: " + required)
    sources = {name: source_file(repo, name) for name in names}
    dirty = {name for name in git(repo, "diff", "--name-only", "-z", "HEAD", "--", "Assets", "Packages", "ProjectSettings").decode("utf-8").split("\0") if name}
    head_quality = git(repo, "show", "HEAD:" + QUALITY)
    reject_sensitive_content(head_quality, QUALITY)
    destination.parent.mkdir(parents=True, exist_ok=True)
    records = []
    discrepancies = []
    # Stage beside the target. On failure TemporaryDirectory removes only the
    # newly created staging folder, never a source or an existing destination.
    with tempfile.TemporaryDirectory(prefix=".ios-source-", dir=destination.parent) as temporary:
        staging = Path(temporary)
        for name in names:
            reject_sensitive_name(name)
            target = staging / name
            target.parent.mkdir(parents=True, exist_ok=True)
            if name == QUALITY:
                target.write_bytes(head_quality)
                size, digest = len(head_quality), hashlib.sha256(head_quality).hexdigest()
                chosen = "HEAD"
            else:
                with target.open("xb") as sink:
                    size, digest = transfer(sources[name], sink, name)
                chosen = "working-tree"
            records.append({"path": name, "bytes": size, "sha256": digest, "source": chosen})
            if name in dirty or name not in tracked:
                _, working_hash = transfer(sources[name], None, name)
                discrepancy = {"path": name, "status": "modified" if name in tracked else "required-overlay",
                               "selectedSource": chosen, "packagedSha256": digest, "workingTreeSha256": working_hash}
                if name in tracked:
                    try:
                        discrepancy["headSha256"] = hashlib.sha256(git(repo, "show", "HEAD:" + name)).hexdigest()
                    except PackageError:
                        discrepancy["status"] = "tracked-addition"
                discrepancies.append(discrepancy)
        document = {"formatVersion": 1, "kind": "portable-unity-source", "rootCommit": commit,
                    "fileCount": len(records), "totalBytes": sum(record["bytes"] for record in records),
                    "qualitySettingsPolicy": "HEAD bytes; excluded working changes are never copied",
                    "sourceDiscrepancies": discrepancies, "files": records}
        (staging / SOURCE_MANIFEST).write_bytes(json_bytes(document))
        (staging / SOURCE_MARKER).write_text("Portable Unity iOS source snapshot\nRoot commit: " + commit + "\n", encoding="utf-8")
        if git(repo, "rev-parse", "HEAD").decode("ascii").strip() != commit:
            raise PackageError("Source HEAD changed during prepare; retry from a stable checkout.")
        if destination.exists():
            # rmdir fails safely if somebody filled it while prepare was running.
            destination.rmdir()
        staging.rename(destination)
    return {"destination": str(destination), "rootCommit": commit, "fileCount": len(records),
            "totalBytes": document["totalBytes"], "manifest": SOURCE_MANIFEST,
            "sourceDiscrepancies": len(discrepancies)}


def enumerate_files(root: Path) -> list[tuple[str, Path]]:
    """Dereference internal links, including Windows junctions, and reject escapes/cycles."""
    files = []

    def visit(folder: Path, prefix: str, ancestors: frozenset[Path]) -> None:
        try:
            resolved = folder.resolve(strict=True)
        except (OSError, RuntimeError) as error:
            raise PackageError("Invalid link in export/support: " + prefix) from error
        if not inside(resolved, root):
            raise PackageError("Link escapes export/support root: " + prefix)
        if resolved in ancestors:
            raise PackageError("Directory link cycle: " + prefix)
        lineage = ancestors | {resolved}
        for child in sorted(folder.iterdir(), key=lambda item: item.name):
            name = relative_name(prefix + child.name)
            reject_sensitive_name(name)
            try:
                target = child.resolve(strict=True)
            except (OSError, RuntimeError) as error:
                raise PackageError("Broken or invalid link: " + name) from error
            if not inside(target, root):
                raise PackageError("Link escapes export/support root: " + name)
            if target.is_dir():
                visit(child, name + "/", lineage)
            elif target.is_file() and stat.S_ISREG(target.stat().st_mode):
                files.append((name, target))
            else:
                raise PackageError("Non-regular export/support entry: " + name)

    visit(root, "", frozenset())
    collision_check([name for name, _ in files])
    return files


def package(export: Path, support: Path, output: Path) -> dict:
    export, support = export.resolve(strict=True), support.resolve(strict=True)
    if not export.is_dir() or not support.is_dir():
        raise PackageError("--export and --support must be directories.")
    if not (export / "Unity-iPhone.xcodeproj/project.pbxproj").is_file():
        raise PackageError("Expected Unity-iPhone.xcodeproj/project.pbxproj in --export.")
    for required in ("codemagic.yaml", "LEEME.md"):
        if not (support / required).is_file():
            raise PackageError("Required support file is missing: " + required)
    output = output.absolute()
    if output.exists() or output.is_symlink():
        raise PackageError("--output already exists; it will not be overwritten.")
    if inside(output.resolve(), export) or inside(output.resolve(), support):
        raise PackageError("--output must be outside --export and --support.")
    if inside(export, support) or inside(support, export):
        raise PackageError("--export and --support must not overlap.")
    exports = [("ios/" + name, path, "xcode") for name, path in enumerate_files(export)]
    supports = [(name, path, "support") for name, path in enumerate_files(support)]
    if any(name == "ios" or name.startswith("ios/") for name, _, _ in supports):
        raise PackageError("Support files may not occupy the reserved ios/ prefix.")
    inputs = sorted(exports + supports, key=lambda item: item[0])
    collision_check([name for name, _, _ in inputs] + [ZIP_MANIFEST])
    output.parent.mkdir(parents=True, exist_ok=True)
    records = []
    with tempfile.TemporaryDirectory(prefix=".ios-zip-", dir=output.parent) as temporary:
        archive = Path(temporary) / "package.zip"
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as zipper:
            for name, path, kind in inputs:
                info = zipfile.ZipInfo(name)
                info.create_system = 3
                # Regular file bits, never symlink bits. Shell helpers stay executable after extraction on macOS.
                mode = 0o755 if path.suffix.lower() == ".sh" or path.stat().st_mode & 0o111 else 0o644
                info.external_attr = (stat.S_IFREG | mode) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                with zipper.open(info, "w", force_zip64=True) as sink:
                    size, digest = transfer(path, sink, name)
                records.append({"path": name, "bytes": size, "sha256": digest, "source": kind})
            document = {"formatVersion": 1, "kind": "codemagic-unity-xcode",
                        "xcodeProject": "ios/Unity-iPhone.xcodeproj", "fileCount": len(records),
                        "totalBytes": sum(record["bytes"] for record in records), "files": records}
            zipper.writestr(ZIP_MANIFEST, json_bytes(document))
        with zipfile.ZipFile(archive, "r") as zipper:
            bad_file = zipper.testzip()
            if bad_file is not None:
                raise PackageError("ZIP CRC verification failed for " + bad_file)
            if len(zipper.infolist()) != len(records) + 1:
                raise PackageError("ZIP entry count does not match its manifest.")
        # Exclusive creation also prevents a race from overwriting somebody's output.
        created = False
        try:
            with output.open("xb") as sink, archive.open("rb") as source:
                created = True
                shutil.copyfileobj(source, sink, CHUNK)
        except Exception:
            if created:
                output.unlink(missing_ok=True)
            raise
    digest = hashlib.sha256()
    with output.open("rb") as stream:
        for block in iter(lambda: stream.read(CHUNK), b""):
            digest.update(block)
    return {"output": str(output), "zipBytes": output.stat().st_size, "sha256": digest.hexdigest(),
            "fileCount": len(records), "manifest": ZIP_MANIFEST, "crcVerified": True}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    snapshot = commands.add_parser("prepare", help="Materialize tracked Unity sources without machine-specific junctions.")
    snapshot.add_argument("--destination", required=True, type=Path, help="New absolute directory, or an existing empty directory.")
    archive = commands.add_parser("package", help="Create a credential-free ZIP64 of an existing Xcode export and support files.")
    archive.add_argument("--export", required=True, type=Path, dest="export_path")
    archive.add_argument("--support", required=True, type=Path)
    archive.add_argument("--output", required=True, type=Path, help="New ZIP path; never overwritten.")
    arguments = parser.parse_args(argv)
    try:
        if arguments.command == "prepare":
            result = prepare(Path(__file__).resolve().parent.parent, arguments.destination)
        else:
            result = package(arguments.export_path, arguments.support, arguments.output)
    except (PackageError, OSError, UnicodeError) as error:
        # PackageError text contains paths/categories only, not captured secrets or subprocess stderr.
        print("Packaging failed: " + str(error), file=sys.stderr)
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
