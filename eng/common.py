"""Process receipts and an owned, disposable PostgreSQL fixture. Python 3.12+."""
from contextlib import contextmanager
from pathlib import Path
import hashlib
import json
import os
import secrets
import subprocess
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
IMAGE = "postgres:18.7@sha256:2ea8630df176ad090ec92136dee850397c5af8781e4bcf2e4162593691c61bbc"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n")


def capture(args, cwd=ROOT, env=None, data=None, timeout=300):
    return subprocess.run(args, cwd=cwd, env=env, input=data, text=True,
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)


def run(args, log, cwd=ROOT, env=None, expected=(0,), timeout=600):
    result = capture(args, cwd, env, timeout=timeout)
    log = Path(log)
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text(result.stdout)
    receipt = {"command": [str(x) for x in args], "exitCode": result.returncode,
               "log": str(log), "sha256": digest(log)}
    write(log.with_suffix(".receipt.json"), receipt)
    if result.returncode not in expected:
        raise RuntimeError(f"{args[0]} exited {result.returncode}; inspect {log}")
    return receipt


@contextmanager
def postgres():
    """Never accepts a user DSN; only removes the container it created."""
    name = "fsplayground-" + uuid.uuid4().hex[:12]
    database = "fsplayground_" + uuid.uuid4().hex[:12]
    environment = dict(os.environ, POSTGRES_PASSWORD=secrets.token_hex(24))
    created = False
    try:
        result = capture(["docker", "run", "--detach", "--rm", "--name", name,
                          "--label", "org.canonflow.fixture=fsharp-playground",
                          "--publish", "127.0.0.1::5432", "--env", "POSTGRES_PASSWORD",
                          "--env", f"POSTGRES_DB={database}", IMAGE], env=environment)
        if result.returncode:
            raise RuntimeError("Unable to create the owned PostgreSQL fixture: " + result.stdout)
        created = True
        deadline = time.monotonic() + 60
        while capture(["docker", "exec", name, "pg_isready", "-U", "postgres", "-d", database]).returncode:
            if time.monotonic() > deadline:
                raise RuntimeError("PostgreSQL fixture did not become ready")
            time.sleep(.25)
        # pg_isready can briefly see the image's bootstrap server before restart.
        port = capture(["docker", "port", name, "5432/tcp"]).stdout.strip().split(":")[-1]
        time.sleep(1)
        connection = (f"Host=127.0.0.1;Port={port};Database={database};Username=postgres;"
                      f"Password={environment['POSTGRES_PASSWORD']};Timeout=5;Command Timeout=5")
        yield name, dict(os.environ, FSPLAYGROUND_POSTGRES=connection)
    finally:
        if created:
            capture(["docker", "rm", "--force", name])


def schema_replay(container, output):
    """Initial schemas only; drift/migration/adversarial scripts stay explicit exclusions."""
    paths = sorted((ROOT / "samples/10-enterprise").glob("**/01-schema.sql"))
    paths.append(ROOT / "samples/10-enterprise/schema.sql")
    receipts = []
    for index, path in enumerate(paths):
        database = f"fsplayground_schema_{index}"
        create = capture(["docker", "exec", container, "createdb", "-U", "postgres", database])
        if create.returncode:
            raise RuntimeError("Failed to create an isolated schema database")
        result = capture(["docker", "exec", "-i", container, "psql", "-X", "-v",
                          "ON_ERROR_STOP=1", "-U", "postgres", "-d", database], data=path.read_text())
        log = output / f"schema-{index}.log"
        log.write_text(result.stdout)
        receipts.append({"path": str(path.relative_to(ROOT)), "sha256": digest(path),
                         "exitCode": result.returncode, "logSha256": digest(log)})
        if "webuy-core" in path.parts and result.returncode == 0:
            assertions = ROOT / "eng/webuy-constraints.sql"
            checked = capture(["docker", "exec", "-i", container, "psql", "-X", "-v",
                               "ON_ERROR_STOP=1", "-U", "postgres", "-d", database], data=assertions.read_text())
            (output / "webuy-constraints.log").write_text(checked.stdout)
            write(output / "webuy-constraints.json", {"assertionsSha256": digest(assertions), "exitCode": checked.returncode,
                                                       "scope": "10 rejection probes and valid inventory/order/rating writes"})
            if checked.returncode:
                raise RuntimeError("Webuy SQL behavior assertions failed")
    write(output / "schemas.json", {"image": IMAGE, "schemas": receipts})
    if any(item["exitCode"] for item in receipts):
        raise RuntimeError("One or more schema replays failed; inspect schemas.json")

