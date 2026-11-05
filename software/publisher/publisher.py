#!/usr/bin/env python3
"""SigCore incremental web publisher. Source files are authoritative, never remote state.

Python 3.10+. Preview by default; --execute required for remote changes.
Only the product website is published. Device documentation links to the product references.
"""
import argparse
import fnmatch
import hashlib
import json
import os
import shutil
from pathlib import Path, PurePosixPath
import shlex
import subprocess
import sys
import tempfile
from datetime import datetime, timezone

HERE = Path(__file__).resolve().parent
CONFIG = HERE / 'publisher.json'
MANIFEST_VERSION = 1


def abort(message):
    raise SystemExit('ERROR: ' + message)


def sha256(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def load_config():
    data = json.loads(CONFIG.read_text(encoding='utf-8'))
    return data


def source_path(cfg, target):
    folder = (HERE / cfg[f'{target}_source']).resolve()
    if not folder.is_dir():
        abort(f'{target} source not found: {folder}')
    return folder


def excluded(rel, patterns):
    parts = PurePosixPath(rel).parts
    return any(fnmatch.fnmatch(part, pat) or fnmatch.fnmatch(rel, pat)
               for pat in patterns for part in parts)


def scan(root, excludes):
    result = {}
    for path in sorted(root.rglob('*')):
        if path.is_symlink():
            abort(f'Symlink in website: {path}; review explicitly')
        if not path.is_file():
            continue
        rel = path.relative_to(root).as_posix()
        if excluded(rel, excludes):
            continue
        result[rel] = sha256(path)
    return result


def manifest_path(cfg, target):
    directory = (HERE / cfg.get('state_directory', '.publisher-state')).resolve()
    directory.mkdir(parents=True, exist_ok=True)
    return directory / f'{target}.json'


def destination_id(cfg, target):
    d = cfg.get('targets', {}).get(target, {})
    return '|'.join((str(d.get('host', '')), str(d.get('user', '')),
                     str(d.get('port', 22)), str(d.get('remote_path', ''))))


def read_manifest(cfg, target):
    path = manifest_path(cfg, target)
    if not path.exists():
        return {}
    state = json.loads(path.read_text(encoding='utf-8'))
    if state.get('version') != MANIFEST_VERSION:
        abort(f'Unknown manifest version: {path}')
    if state.get('destination') != destination_id(cfg, target):
        abort(f'Deployment target changed for {target}. Use reset-state after verifying destination.')
    return state.get('files', {})


def save_manifest(cfg, target, hashes):
    path = manifest_path(cfg, target)
    data = {'version': MANIFEST_VERSION, 'destination': destination_id(cfg, target),
            'updated_at': datetime.now(timezone.utc).isoformat(), 'files': hashes}
    with tempfile.NamedTemporaryFile('w', encoding='utf-8', dir=path.parent,
                                     prefix=path.name + '.', delete=False) as tmp:
        json.dump(data, tmp, indent=2, sort_keys=True)
        tmp.write('\n')
        name = tmp.name
    os.replace(name, path)


def validate(source, target, hashes):
    warnings = []
    if target == 'product' and 'reference/control-panel-reference.html' not in hashes:
        abort('Missing required reference/control-panel-reference.html')
    needles = (
        ('D88A42', 'obsolete device name'),
        ('120VAC power is connected', 'obsolete power advice'),
    )
    for rel in hashes:
        if not rel.lower().endswith(('.html', '.php', '.htm')):
            continue
        for number, line in enumerate((source / rel).read_text(encoding='utf-8-sig', errors='replace').splitlines(), 1):
            for needle, meaning in needles:
                if needle in line:
                    warnings.append(f'{rel}:{number}: {meaning}: {needle}')
            if rel.endswith('control-panel-reference.html') and 'Two analog output channels' in line:
                warnings.append(f'{rel}:{number}: obsolete AO count: Two analog output channels')
    for warning in warnings:
        print('WARNING: ' + warning)
    return warnings

def remote_commands(dest):
    host, user = dest['host'], dest['user']
    if (not host or not user or not dest.get('remote_path') or
            any('CONFIGURE_' in str(v) for v in [host, user, dest['remote_path'], dest.get('identity_file', '')])):
        abort('Configure SSH host, user, remote_path and identity_file before executing.')
    options = ['-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes']
    key = dest.get('identity_file')
    if key:
        key_path = Path(key).expanduser()
        if not key_path.is_absolute():
            key_path = (HERE / key_path).resolve()
        if not key_path.is_file():
            abort(f'SSH deploy key not found: {key_path}')
        options += ['-i', str(key_path)]
    port = str(dest.get('port', 22))
    ssh = ['ssh', *options, '-p', port, f'{user}@{host}']
    scp = ['scp', *options, '-P', port]
    return ssh, scp, f'{user}@{host}'


def deploy_file(root, relative, dest):
    ssh, scp, account = remote_commands(dest)
    base = PurePosixPath(dest['remote_path'])
    if not base.is_absolute() or '..' in base.parts:
        abort('Remote path must be an absolute safe path')
    rel = PurePosixPath(relative)
    if rel.is_absolute() or '..' in rel.parts:
        abort('Unsafe source path')
    target = base / rel
    tmp = PurePosixPath(str(target) + '.sigcore-upload-tmp')
    subprocess.run(ssh + ['mkdir -p -- ' + shlex.quote(str(target.parent))], check=True)
    # Prevent a partially uploaded file from replacing the existing file.
    subprocess.run(scp + [str(root / relative), f'{account}:{shlex.quote(str(tmp))}'], check=True)
    subprocess.run(ssh + ['mv -f -- ' + shlex.quote(str(tmp)) + ' ' + shlex.quote(str(target))], check=True)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('command', choices=['inventory', 'publish', 'reset-state'])
    p.add_argument('--target', choices=['product'], default='product', help='Only the public product website is published')
    p.add_argument('--execute', action='store_true', help='Actually upload; without this only preview')
    p.add_argument('--full', action='store_true', help='Reupload all, regardless of manifest')
    args = p.parse_args()
    cfg = load_config()
    target = 'product'
    source = source_path(cfg, target)
    current = scan(source, cfg.get('exclude', []))
    if args.command == 'inventory':
        print(f'product: {len(current)} files in {source}')
        return
    if args.command == 'reset-state':
        if args.execute:
            manifest_path(cfg, target).unlink(missing_ok=True)
            print('Product manifest reset.')
        else:
            print('Would reset product manifest; use --execute.')
        return
    validate(source, target, current)
    previous = read_manifest(cfg, target)
    changed = [rel for rel, digest in current.items() if args.full or previous.get(rel) != digest]
    missing_locally = sorted(set(previous) - set(current))
    print(f'product: {len(current)} files, {len(changed)} to upload, '
          f'{len(current)-len(changed)} unchanged, {len(missing_locally)} absent locally')
    for rel in changed:
        print('  UPLOAD ' + rel)
    for rel in missing_locally:
        print('  REVIEW REMOTE-ONLY (no automatic deletion) ' + rel)
    if not args.execute:
        if not cfg.get('targets', {}).get('product'):
            print('CONFIGURATION NEEDED before live publish: targets.product SSH settings in publisher.json')
        print('DRY RUN. No files transferred or changed.')
        return
    if changed:
        dest = cfg.get('targets', {}).get('product')
        if not dest:
            abort('Hostinger SSH destination is not configured. Set targets.product in publisher.json (host, user, port, remote_path, identity_file). No uploads made.')
        remote_commands(dest)
    state = dict(previous)
    for rel in changed:
        try:
            deploy_file(source, rel, cfg['targets']['product'])
        except (subprocess.CalledProcessError, OSError) as exc:
            abort(f'Upload failed for {rel}: {exc}. Successful uploads already recorded in the local manifest.')
        state[rel] = current[rel]
        save_manifest(cfg, target, state)
    print(f'product: uploaded {len(changed)} files. Device not deployed.')


if __name__ == '__main__':
    main()
