"""Isolated requirement probes; unchanged tests distinguish killed, survived and invalid."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
from common import ROOT, capture, digest, postgres, run, write
from qualify import trx

CASES = [
    ("request-limit", "Wallet.Domain/Domain.fs", "elif amount>limit", "elif amount>=limit", "Killed"),
    ("positive-amount", "Wallet.Domain/Domain.fs", "amount<=0m", "amount<0m", "Killed"),
    ("replay-intent", "Wallet.Adapter/Adapter.fs", "previousWallet=wallet && previousAmount=amount", "previousWallet=wallet", "Killed"),
    ("wire-reference", "Wallet.Adapter/Adapter.fs", 'reference=reference;status="accepted"', 'id=reference;status="accepted"', "Killed"),
    ("error-wording-gap", "Wallet.Adapter/Adapter.fs", 'invalid-debit', 'invalid-amount', "Survived"),
    ("invalid-source", "Wallet.Domain/Domain.fs", "elif amount>limit", "elif amount >", "Invalid"),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    output = parser.parse_args().output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    paths = [*sorted((ROOT / "samples/40-wallet").glob("*/*.fs")), ROOT / "tests/Wallet.Tests/Program.fs"]
    bindings = {str(p.relative_to(ROOT)): digest(p) for p in paths}
    write(output / "source-bindings.json", bindings)
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", TESTINGPLATFORM_TELEMETRY_OPTOUT="1", MSBUILDDISABLENODEREUSE="1")
    project = "tests/Wallet.Tests/Wallet.Tests.fsproj"
    rows = []
    with postgres() as (_, fixture):
        fixture.update({key: env[key] for key in ("DOTNET_CLI_TELEMETRY_OPTOUT", "TESTINGPLATFORM_TELEMETRY_OPTOUT", "MSBUILDDISABLENODEREUSE")})
        run(["dotnet", "run", "--no-build", "--project", project, "-c", "Release", "--", "--report-trx", "--report-trx-filename", "baseline.trx", "--results-directory", str(output)], output / "baseline.log", env=fixture)
        baseline_passed = trx(output / "baseline.trx")["passed"]
        if baseline_passed != 22:
            raise RuntimeError("Mutation baseline must execute all 22 unchanged acceptance tests")
        for name, source, find, replacement, expected in CASES:
            print("Mutation", name, flush=True)
            work = output / name
            work.mkdir()
            for folder in ("samples/40-wallet", "tests/Wallet.Tests"):
                shutil.copytree(ROOT / folder, work / folder, ignore=shutil.ignore_patterns("bin", "obj"))
            for path in ("global.json", "Directory.Build.props", "NuGet.Config"):
                shutil.copy2(ROOT / path, work / path)
            path = work / "samples/40-wallet" / source
            original = path.read_text()
            if original.count(find) != 1:
                raise RuntimeError("Mutation anchor is not unique: " + name)
            path.write_text(original.replace(find, replacement))
            built = run(["dotnet", "build", project, "-c", "Release", "-m:1", "-p:RestoreLockedMode=true"], work / "build.log", work, fixture, expected=(0, 1))
            result = {"id": name, "expected": expected, "source": source, "find": find, "replace": replacement}
            if built["exitCode"]:
                if not re.search(r"error FS\d+:", (work / "build.log").read_text()):
                    raise RuntimeError(f"{name}: build failed without an F# compiler diagnostic")
                result.update(before="Invalid", expanded="Invalid")
            else:
                base = ["dotnet", "run", "--no-build", "--project", project, "-c", "Release", "--", "--report-trx"]
                run([*base, "--filter", "FullyQualifiedName~reference normalization is explicit", "--results-directory", str(work / "before")], work / "before.log", work, fixture)
                before = next((work / "before").glob("*.trx"))
                if trx(before)["passed"] != 1:
                    raise RuntimeError("Weak-suite probe did not execute its single acceptance case")
                executed = run([*base, "--results-directory", str(work / "expanded")], work / "expanded.log", work, fixture, expected=(0, 2))
                import xml.etree.ElementTree as ET
                report = next((work / "expanded").glob("*.trx"))
                counters = ET.parse(report).getroot().find(".//{*}Counters").attrib
                passed, failed = int(counters["passed"]), int(counters["failed"])
                if (int(counters["total"]) != baseline_passed
                        or passed + failed != baseline_passed
                        or int(counters["notExecuted"])
                        or (executed["exitCode"] == 0) != (failed == 0)):
                    raise RuntimeError("Expanded mutation suite did not execute all unchanged tests")
                result.update(before="Survived", expanded="Killed" if int(counters["failed"]) else "Survived", counters=counters)
            rows.append(result)
            write(output / "results.json", rows)
            if result["expanded"] != expected:
                raise RuntimeError("Unexpected mutation result; preserve it and investigate: " + name)
    if any(digest(ROOT / path) != sha for path, sha in bindings.items()):
        raise RuntimeError("Source changed while mutations ran")
    execution = output / "decision-input.json"
    write(execution, {"baselinePassed": baseline_passed, "snapshotMatches": True,
                      "candidates": [{"id": row["id"], "status": row["expanded"],
                                      "passed": int(row.get("counters", {}).get("passed", 0)),
                                      "failed": int(row.get("counters", {}).get("failed", 0))} for row in rows]})
    cli = "samples/50-decision-assist/DecisionAssist.Cli/DecisionAssist.Cli.fsproj"
    log = output / "decision.log"
    run(["dotnet", "run", "--project", cli, "--no-build", "--no-restore", "-c", "Release", "--", str(execution)], log, env=env)
    advice = json.loads(log.read_text())
    expected_actions = {"Killed": "NoInvestigation", "Survived": "SurvivorReview", "Invalid": "RepairCandidate"}
    expected = [(row["id"], expected_actions[row["expanded"]]) for row in rows]
    if [(item["id"], item["action"]) for item in advice["decisions"]] != expected:
        raise RuntimeError("Decision Assist routing did not match executed evidence")
    write(output / "decision.json", advice)
    print("Four killed, one survivor, one invalid. Offline Decision Assist routed all six results.")


if __name__ == "__main__":
    main()
