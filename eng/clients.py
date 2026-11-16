"""Execute every retained Jest client; the Next.js frontend has separate scope."""
import argparse
import json
from pathlib import Path
from common import ROOT, run, write


def qualify(output):
    output.mkdir(parents=True, exist_ok=False)
    receipts = []
    for index, package in enumerate(sorted((ROOT / "samples/10-enterprise").glob("**/package.json"))):
        if "node_modules" in package.parts or package.parent.name != "client":
            continue
        definition = json.loads(package.read_text())
        if definition["scripts"].get("test") != "jest":
            raise RuntimeError(f"Unreviewed test command in {package}")
        run(["npm", "ci", "--ignore-scripts", "--no-audit", "--no-fund"], output / f"install-{index}.log", package.parent)
        report = output / f"tests-{index}.json"
        run(["npm", "test", "--", "--runInBand", "--json", "--outputFile", str(report)], output / f"tests-{index}.log", package.parent)
        data = json.loads(report.read_text())
        if not data.get("success") or data["numPassedTests"] < 1 or data["numPendingTests"]:
            raise RuntimeError(f"Missing, failed or skipped client tests in {package}")
        receipts.append({"client": str(package.parent.relative_to(ROOT)), "passed": data["numPassedTests"],
                         "limit": "Generated validator tests include dummy properties; passing is not end-to-end coverage"})
    write(output / "summary.json", receipts)
    print(json.dumps(receipts, indent=2), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    qualify(parser.parse_args().output.resolve())
