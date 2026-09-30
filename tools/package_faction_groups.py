"""Package the finished Windows player; never builds, starts Unity, or runs games.

Run only after the Unity build has completed:
    python tools/package_faction_groups.py --build-guid <optional-Unity-build-GUID>
Use --check-inputs to inspect the package inputs without writing a ZIP.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Builds/Windows"
DESTINATION = ROOT / "Artifacts/ArtReview/faction-groups"
README = """EMBERFIELD - FACCIONES

Extrae todo el ZIP en una carpeta y abre Emberfield.exe. Mant\xe9n junto al
ejecutable Emberfield_Data, UnityPlayer.dll y las otras carpetas del paquete.

En Escaramuza elige Hist\xf3ricas, Fantas\xeda o Navales y despu\xe9s una facci\xf3n.
Activas: Franceses, Hispanos, Ingleses; Orcos, Enanos, Hombres de las
monta\xf1as, Elfos; Piratas. Marina inglesa, Marina espa\xf1ola y Flota esquel\xe9tica
aparecen como pendientes. Navales usa combate terrestre en Sapphire Coast;
todav\xeda no hay navegaci\xf3n ni combate de barcos. El arte nacional es compartido
y los Orcos todav\xeda no tienen modelos propios terminados.

Online: la direcci\xf3n p\xfablica del servidor alfa es temporal y depende del PC
que lo aloja. En el proyecto local puedes iniciar el cliente con la direcci\xf3n
actual usando tools/Play-OnlineAlpha.ps1. En otro PC, abre ONLINE y usa la
direcci\xf3n vigente que facilite el anfitri\xf3n. No se garantiza que una direcci\xf3n
incluida en un paquete anterior siga disponible.

Esta compilaci\u00f3n conserva los controles de voz y las facciones. El cambio
de idioma de otra tarea segu\u00eda en curso al preparar la copia de fuentes y
no se incluye aqu\u00ed; parte de la interfaz permanece en ingl\u00e9s.

package-verification.json verifica CRC y SHA-256 del paquete. Esa verificaci\xf3n
no sustituye las pruebas de juego, red o calidad visual.
"""


def excluded(relative: Path) -> bool:
    for part in relative.parts:
        lower = part.lower()
        if ("donotship" in lower or "dontshipitwithyourgame" in lower
                or "burstdebuginformation" in lower
                or lower in {"debug", ".debug", "debugsymbols", "symbols"}):
            return True
    return relative.suffix.lower() in {".pdb", ".mdb", ".dbg", ".ilk"}


def sha256(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def input_files() -> list[Path]:
    if not SOURCE.is_dir():
        raise FileNotFoundError("Builds/Windows is missing; complete the Unity build first.")
    files = sorted((p for p in SOURCE.rglob("*") if p.is_file()
                    and not excluded(p.relative_to(SOURCE))),
                   key=lambda p: p.relative_to(SOURCE).as_posix())
    names = {p.relative_to(SOURCE).as_posix() for p in files}
    required = {"Emberfield.exe", "UnityPlayer.dll", "Emberfield_Data/globalgamemanagers"}
    if not required.issubset(names) or not any(n.startswith("Emberfield_Data/") for n in names):
        raise ValueError("Player is incomplete: executable, UnityPlayer.dll and Data are required.")
    with (SOURCE / "Emberfield.exe").open("rb") as executable:
        if executable.read(2) != b"MZ":
            raise ValueError("Emberfield.exe is not a Windows executable.")
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-inputs", action="store_true")
    parser.add_argument("--build-guid", default="")
    args = parser.parse_args()
    if args.build_guid and not re.fullmatch(r"[A-Fa-f0-9]{32}", args.build_guid):
        parser.error("--build-guid must be Unity's 32-character hexadecimal build GUID.")
    files = input_files()
    if args.check_inputs:
        print(json.dumps({"ready": True, "files": len(files),
                          "bytes": sum(p.stat().st_size for p in files),
                          "writes": False}))
        return

    DESTINATION.mkdir(parents=True, exist_ok=True)
    package = DESTINATION / "Emberfield-Windows.zip"
    temporary = DESTINATION / "Emberfield-Windows.zip.partial"
    report_path = DESTINATION / "package-verification.json"
    metadata = {p.relative_to(SOURCE).as_posix(): (p.stat().st_size, p.stat().st_mtime_ns)
                for p in files}
    records = []
    try:
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED,
                             compresslevel=5, allowZip64=True) as archive:
            for path in files:
                name = path.relative_to(SOURCE).as_posix()
                archive.write(path, name)
                records.append({"path": name, "bytes": path.stat().st_size,
                                "sha256": sha256(path)})
            archive.writestr("LEEME-FACCIONES.txt", README.encode("utf-8"))
        for path in files:
            name = path.relative_to(SOURCE).as_posix()
            if (path.stat().st_size, path.stat().st_mtime_ns) != metadata[name]:
                raise RuntimeError("Build changed during packaging: " + name)
        if [p.relative_to(SOURCE) for p in input_files()] != [p.relative_to(SOURCE) for p in files]:
            raise RuntimeError("Build file list changed during packaging.")

        with zipfile.ZipFile(temporary, "r") as archive:
            bad = archive.testzip()
            if bad is not None:
                raise RuntimeError("ZIP CRC verification failed: " + bad)
            names = archive.namelist()
            if len(names) != len(set(names)) or len(names) != len(files) + 1:
                raise RuntimeError("ZIP contains missing or duplicated entries.")
            for record in records:
                with archive.open(record["path"]) as stream:
                    if hashlib.file_digest(stream, "sha256").hexdigest() != record["sha256"]:
                        raise RuntimeError("ZIP content differs from source: " + record["path"])
            if archive.read("LEEME-FACCIONES.txt") != README.encode("utf-8"):
                raise RuntimeError("README verification failed.")
        digest = sha256(temporary)
        temporary.replace(package)
        (DESTINATION / "README.txt").write_text(README, encoding="utf-8")
        report = {
            "passed": True,
            "generatedUtc": datetime.now(timezone.utc).isoformat(),
            "buildGuidProvidedByCaller": args.build_guid or None,
            "package": str(package.relative_to(ROOT)).replace("\\", "/"),
            "bytes": package.stat().st_size,
            "sha256": digest,
            "entries": len(files) + 1,
            "crcVerified": True,
            "allSourceHashesVerified": True,
            "requiredPlayerFilesPresent": True,
            "scope": "Finished player package integrity only; no Unity, network, gameplay or visual tests were executed by this script.",
            "excluded": ["*DoNotShip*", "*DontShipItWithYourGame*", "BurstDebugInformation", "debug/symbol directories", ".pdb", ".mdb", ".dbg", ".ilk"],
            "files": records,
        }
        report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({"passed": True, "package": report["package"],
                          "entries": report["entries"], "bytes": report["bytes"],
                          "sha256": digest}))
    except Exception as error:
        if temporary.exists():
            temporary.unlink()
        report_path.write_text(json.dumps({"passed": False,
            "generatedUtc": datetime.now(timezone.utc).isoformat(),
            "error": str(error), "scope": "Package integrity verification."}, indent=2) + "\n", encoding="utf-8")
        raise


if __name__ == "__main__":
    main()
