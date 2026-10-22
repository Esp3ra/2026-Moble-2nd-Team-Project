"""Reviewed Git tree export for C# + server/Pi integration, never checkout a Draft."""
import argparse
import io
import json
from pathlib import Path
import re
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[2]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--alpha-ref", required=True)
    p.add_argument("--peer-ref", required=True)
    p.add_argument("--output", type=Path, required=True)
    args = p.parse_args()
    def git(*parts): return subprocess.check_output(["git", *parts], cwd=ROOT)
    alpha = git("rev-parse", "--verify", args.alpha_ref + "^{commit}").decode().strip()
    peer = git("rev-parse", "--verify", args.peer_ref + "^{commit}").decode().strip()
    merged = subprocess.run(["git", "merge-tree", "--write-tree", alpha, peer], cwd=ROOT, text=True, capture_output=True)
    if merged.returncode != 0: raise RuntimeError("Combined Draft conflicts require explicit review")
    tree = merged.stdout.splitlines()[0]
    if not re.fullmatch(r"[0-9a-f]{40}", tree): raise RuntimeError("Invalid tree")
    output = args.output.absolute()
    if output.exists() or output == ROOT or ROOT in output.parents: raise RuntimeError("New external test directory required")
    raw = git("archive", tree, "RemoteWork.WinForms", "RemoteWork.sln", "RemoteWork.Server", "Tests", "Devices", "Tools")
    with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
        if any(Path(m.name).name == ".env" or not (m.isfile() or m.isdir()) for m in archive.getmembers()):
            raise RuntimeError("Private environment or unexpected file type in tracked export")
        output.mkdir(parents=True)
        archive.extractall(output, filter="data")
    manifest = dict(alpha=alpha, peer=peer, merge_tree=tree, resolved=[], test_only=True)
    (output / "source-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(dict(source_root=str(output), **manifest)))


if __name__ == "__main__": main()
