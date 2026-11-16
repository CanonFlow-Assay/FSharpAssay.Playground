"""One reproducible gate: compilation, legacy tests, real DB/HTTP, and packaged FsAssay."""
import argparse
import json
import os
from pathlib import Path
import xml.etree.ElementTree as ET
from common import ROOT, capture, digest, postgres, run, schema_replay, write


def trx(path):
    root = ET.parse(path).getroot()
    counters = root.find(".//{*}Counters")
    if counters is None:
        raise RuntimeError(f"No executed test counters in {path}")
    values = {k: int(v) for k, v in counters.attrib.items()}
    if values.get("passed", 0) < 1 or values.get("total") != values.get("passed"):
        raise RuntimeError(f"Missing, failed or skipped tests in {path}")
    return values


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--development", action="store_true", help="omit clean-commit FsAssay authority step")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", TESTINGPLATFORM_TELEMETRY_OPTOUT="1",
               MSBUILDDISABLENODEREUSE="1")
    run(["python3", "eng/provenance.py"], output / "provenance.log", env=env)
    expected = json.loads((ROOT / "global.json").read_text())["sdk"]["version"]
    if capture(["dotnet", "--version"]).stdout.strip() != expected:
        raise RuntimeError("Exact global.json SDK required")
    projects = sorted(str(p.relative_to(ROOT)) for folder in ("samples", "tests")
                      for p in (ROOT / folder).rglob("*.fsproj") if not {"bin", "obj", "node_modules"}.intersection(p.parts))
    declared = sorted(p.attrib["Path"] for p in ET.parse(ROOT / "FSharpAssay.Playground.slnx").getroot())
    if declared != projects:
        raise RuntimeError("Solution must inventory every active F# project")
    run(["dotnet", "restore", "FSharpAssay.Playground.slnx", "--locked-mode", "-m:1"], output / "restore.log", env=env)
    run(["dotnet", "build", "FSharpAssay.Playground.slnx", "--no-restore", "-c", "Release", "-m:1"], output / "build.log", env=env)
    run(["dotnet", "list", "FSharpAssay.Playground.slnx", "package", "--vulnerable", "--include-transitive", "--format", "json"], output / "dependency-audit.json", env=env)
    audit = json.loads((output / "dependency-audit.json").read_text())
    if audit.get("problems") or len(audit.get("projects", [])) != len(projects):
        raise RuntimeError("Incomplete NuGet advisory evidence")
    if any(framework.get("topLevelPackages") or framework.get("transitivePackages") for project in audit["projects"] for framework in project.get("frameworks", [])):
        raise RuntimeError("NuGet advisory findings require review; inspect dependency-audit.json")
    counts = {}
    for index, project in enumerate(p for p in projects if "10-enterprise" in p and "/tests/" in p):
        if "BankingCore.Tests" in project:
            run(["dotnet", "run", "--no-build", "--project", project, "-c", "Release", "--", "--summary"],
                output / "legacy-banking.log", env=env)
            import re
            plain = re.sub(r"\x1b\[[0-?]*[ -/]*[@-~]", "", (output / "legacy-banking.log").read_text())
            match = re.search(r"Passed:\s*(\d+)", plain)
            if not match or int(match.group(1)) != 8:
                raise RuntimeError("Banking suite did not execute all eight retained cases")
            counts[project] = {"passed": 8, "adapter": "Expecto console", "limit": "includes demonstration exploit and generator checks"}
        else:
            results = output / f"legacy-{index}"
            run(["dotnet", "test", project, "--no-build", "--no-restore", "-c", "Release",
                 "--logger", "trx;LogFileName=tests.trx", "--results-directory", str(results)],
                output / f"legacy-{index}.log", env=env)
            counts[project] = trx(results / "tests.trx")
    with postgres() as (container, fixture_env):
        fixture_env.update({key: env[key] for key in ("DOTNET_CLI_TELEMETRY_OPTOUT", "TESTINGPLATFORM_TELEMETRY_OPTOUT", "MSBUILDDISABLENODEREUSE")})
        schema_replay(container, output)
        for name in ("Playground.Acceptance", "Patterns.Tests", "Wallet.Tests"):
            project = f"tests/{name}/{name}.fsproj"
            results = output / name
            run(["dotnet", "run", "--project", project, "--no-build", "-c", "Release", "--", "--report-trx",
                 "--report-trx-filename", "tests.trx", "--results-directory", str(results)], output / f"{name}.log", env=fixture_env)
            counts[project] = trx(results / "tests.trx")
        if not args.development:
            if capture(["git", "status", "--porcelain"]).stdout.strip():
                raise RuntimeError("Authority requires a clean committed candidate")
            producer = json.loads((ROOT / ".packages/producer.json").read_text())
            pin = json.loads((ROOT / "workflows/assay.json").read_text())
            if any(producer.get(key) != value for key, value in pin.items()):
                raise RuntimeError("Local producer does not match the pinned source")
            if digest(ROOT / ".packages/FsAssay.Cli.1.0.4.nupkg") != producer["archiveSha256"]:
                raise RuntimeError("Producer archive changed")
            tool = str(ROOT / ".tools/fsassay")
            for mode, expected_exit in (("verify", 0), ("check", 2)):
                destination = output / mode
                destination.mkdir()
                command = [tool, mode, "--out-json", str(destination / "receipt.json"),
                           "--out-sarif", str(destination / "findings.sarif"),
                           "--assessment-json", str(destination / "assessment.json"),
                           "--ratecard-md", str(destination / "report.md"),
                           "--material-html", str(destination / "report.html")]
                if mode == "verify":
                    command += ["--test-artifacts", str(destination / "tests")]
                command += ["FSharpAssay.Playground.slnx"]
                run(command, output / (mode + ".log"), env=fixture_env, expected=(expected_exit,), timeout=900)
            write(output / "producer.json", producer)
    summary = {"candidate": capture(["git", "rev-parse", "HEAD"]).stdout.strip(),
               "sdk": expected, "projects": len(projects), "tests": counts,
               "authorityRequested": not args.development,
               "limits": ["Legacy dummy properties are not behavioral coverage", "Zero admitted analyzer blocking/advisory rules",
                          "Frontend and historical legacy playground excluded from active qualification",
                          "Drift, migrations and hostile SQL retained as historical inputs, not schema replay passes"]}
    write(output / "summary.json", summary)
    print(json.dumps(summary, indent=2), flush=True)


if __name__ == "__main__":
    main()
