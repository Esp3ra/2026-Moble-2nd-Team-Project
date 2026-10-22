"""Export reviewed Git refs only. Never edit/merge the user's checkout or .env."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--alpha-ref", required=True)
    parser.add_argument("--server-ref", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    def git(*parts):
        return subprocess.check_output(["git", *parts], cwd=ROOT)
    alpha = git("rev-parse", "--verify", args.alpha_ref + "^{commit}").decode().strip()
    peer = git("rev-parse", "--verify", args.server_ref + "^{commit}").decode().strip()
    merge = subprocess.run(["git", "merge-tree", "--write-tree", alpha, peer], cwd=ROOT, capture_output=True, text=True)
    if merge.returncode not in (0, 1): raise RuntimeError("Source merge-tree failed")
    tree = merge.stdout.splitlines()[0]
    if not re.fullmatch(r"[0-9a-f]{40}", tree): raise RuntimeError("No valid review tree")
    names = set(re.findall(r"^CONFLICT .*?: Merge conflict in (.+)$", merge.stdout, re.M))
    expected = {"RemoteWork.Server/.env.example", "RemoteWork.Server/remotework/__init__.py"}
    if names and names != expected: raise RuntimeError("Unexpected conflicts require manual source review")
    output = args.output.absolute()
    if output.exists(): raise RuntimeError("Output must be a new directory")
    output.mkdir(parents=True)
    raw = git("archive", tree, "RemoteWork.Server", "Devices/RaspberryPi")
    with tarfile.open(fileobj=io.BytesIO(raw)) as archive: archive.extractall(output, filter="data")
    resolved = []
    for name in sorted(names):
        path = output / name
        original = path.read_text(encoding="utf-8")
        pattern = r"^<<<<<<< [^\n]*\n(.*?)^=======\n(.*?)^>>>>>>> [^\n]*\n"
        hunks = re.findall(pattern, original, re.M | re.S)
        if len(hunks) != 1: raise RuntimeError("Unexpected conflict hunk count")
        left, right = hunks[0]
        if name.endswith("__init__.py"):
            if "Collaboration requires explicit common write support" not in left or "identity_profiles = validate_configuration" not in right:
                raise RuntimeError("Independent startup guards require manual review")
        else:
            if "COLLABORATION" not in left or "IDENTITY" not in right:
                raise RuntimeError("Unexpected example settings conflict")
        clean = re.sub(pattern, lambda m: m.group(1) + m.group(2), original, flags=re.M | re.S)
        if "<<<<<<<" in clean or ">>>>>>>" in clean: raise RuntimeError("Unresolved source conflict")
        path.write_text(clean, encoding="utf-8", newline="\n")
        resolved.append(dict(path=name, blocks=1, sha256=hashlib.sha256(clean.encode()).hexdigest()))
    if (output / "RemoteWork.Server/.env").exists(): raise RuntimeError("Private configuration in source export")
    manifest = dict(alpha=alpha, peer=peer, merge_tree=tree, resolved=resolved,
                    test_only_conflict_resolution="Both reviewed startup guards/settings retained in export only")
    (output / "source-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(dict(source_root=str(output), alpha=alpha, peer=peer, resolved_files=len(resolved))))


if __name__ == "__main__": main()
