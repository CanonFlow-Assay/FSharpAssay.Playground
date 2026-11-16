"""Verify retained imports against their recorded adaptation disposition."""
import argparse
import json
from common import ROOT, digest, write


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--record", action="store_true", help="explicitly record a reviewed adaptation")
    args = parser.parse_args()
    manifest = json.loads((ROOT / "docs/import-manifest.json").read_text())["files"]
    records = []
    for item in manifest:
        path = ROOT / item["path"]
        if not path.is_file():
            raise RuntimeError("Missing import: " + item["path"])
        current = digest(path)
        records.append({"path": item["path"], "originalSha256": item["sha256"], "currentSha256": current,
                        "disposition": "retained" if current == item["sha256"] else "adapted; see docs/scrutiny.md"})
    destination = ROOT / "docs/migration-changes.json"
    if args.record:
        write(destination, records)
    elif records != json.loads(destination.read_text()):
        raise RuntimeError("Import drift: review the change before explicitly recording it")
    print(f"{len(records)} retained imports; {sum(r['disposition'] != 'retained' for r in records)} adapted files")


if __name__ == "__main__":
    main()
