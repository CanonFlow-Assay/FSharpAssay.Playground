"""Build and install the exact pinned FsAssay source as an isolated package consumer."""
import argparse
import os
from pathlib import Path
import shutil
from common import ROOT, capture, digest, run, write
import json


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--engine", type=Path, required=True)
    args = parser.parse_args()
    engine = args.engine.resolve()
    pin = json.loads((ROOT / "workflows/assay.json").read_text())
    if capture(["dotnet", "--version"]).stdout.strip() != pin["sdk"]:
        raise RuntimeError("Use the exact SDK from global.json")
    if capture(["git", "rev-parse", "HEAD"], engine).stdout.strip() != pin["revision"]:
        raise RuntimeError("Engine checkout does not match workflows/assay.json")
    if capture(["git", "status", "--porcelain"], engine).stdout.strip():
        raise RuntimeError("Engine checkout must be clean")
    packages = ROOT / ".packages"
    packages.mkdir(exist_ok=True)
    logs = ROOT / "artifacts/package"
    logs.mkdir(parents=True, exist_ok=True)
    run(["dotnet", "restore", "FsAssay.Stable.slnx", "--locked-mode", "-m:1"], logs / "restore.log", engine)
    run(["dotnet", "pack", "FsAssay.Runner/FsAssay.Runner.fsproj", "-c", "Release", "--no-restore",
         "-m:1", "--output", str(packages)], logs / "pack.log", engine)
    archive = packages / f"{pin['package']}.{pin['version']}.nupkg"
    # A source revision may reuse the same package version: never reuse the global cache.
    cache = packages / ("cache-" + digest(archive)[:16])
    config = packages / "NuGet.Config"
    from xml.sax.saxutils import escape
    config.write_text('<configuration><packageSources><clear/><add key="producer" value="' +
                      escape(str(packages), {'"': '&quot;'}) + '"/></packageSources></configuration>\n')
    tools = ROOT / ".tools"
    if tools.exists():
        shutil.rmtree(tools)
    env = dict(os.environ, NUGET_PACKAGES=str(cache))
    run(["dotnet", "tool", "install", pin["package"], "--version", pin["version"],
         "--tool-path", str(tools), "--configfile", str(config), "--no-cache"], logs / "install.log", env=env)
    write(packages / "producer.json", {**pin, "archiveSha256": digest(archive),
                                      "consumption": "isolated local archive; no registry publication"})
    print("Installed pinned local package", pin["revision"], digest(archive))


if __name__ == "__main__":
    main()
