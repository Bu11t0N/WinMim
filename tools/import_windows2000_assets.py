"""Stage pinned Windows 2000 resource candidates for a PRIVATE test machine.

Reads ICO data only. Never loads archive DLLs/EXEs or imports registry scripts.
The source collection claims Windows 2000 provenance; original-media fidelity
still requires comparison. These Microsoft images are not public GPL artwork.
"""
from pathlib import Path
import argparse, hashlib, json, struct


def prepare(icons, w2k, output, reference=None):
    reference = reference or Path(__file__).resolve().parents[1] / 'content/windows2000/private-resource-reference.json'
    manifest = json.loads(reference.read_text(encoding='utf-8'))
    staged = {}
    for item in manifest['files']:
        prefix, relative = item['input'].split('/', 1)
        source_root = (icons if prefix == 'icons' else w2k).resolve()
        source = (source_root / relative).resolve()
        if not source.is_relative_to(source_root):
            raise ValueError('Resource escapes source root')
        data = source.read_bytes()
        if len(data) != item['size'] or hashlib.sha256(data).hexdigest() != item['sha256']:
            raise ValueError('Resource hash/size mismatch: ' + item['name'])
        name = Path(item['name'])
        if name.name != item['name'] or name.suffix.lower() not in ('.ico', '.cur', '.wav'):
            raise ValueError('Invalid destination resource name')
        if name.suffix.lower() == '.wav':
            if len(data) < 44 or data[:4] != b'RIFF' or data[8:12] != b'WAVE' or struct.unpack_from('<I', data, 4)[0] + 8 != len(data):
                raise ValueError('Invalid WAVE data')
            staged[name.name] = data
            continue
        magic = b'\0\0\1\0' if name.suffix.lower() == '.ico' else b'\0\0\2\0'
        if data[:4] != magic or len(data) < 22:
            raise ValueError('Invalid icon/cursor: ' + item['name'])
        count = struct.unpack_from('<H', data, 4)[0]
        if not count or 6 + 16 * count > len(data):
            raise ValueError('Invalid ICO directory')
        for index in range(count):
            size, offset = struct.unpack_from('<II', data, 14 + 16 * index)
            if offset < 6 + 16 * count or size == 0 or offset + size > len(data):
                raise ValueError('Invalid ICO image bounds')
        staged[name.name] = data
    # Validate the entire input before creating any output. Never overwrite a pack.
    output.mkdir(parents=True, exist_ok=False)
    for name, data in staged.items():
        (output / name).write_bytes(data)
    (output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'NOTICE.txt').write_text(manifest['license'] + '\n' + manifest['source'] + '\n', encoding='utf-8')
    return len(staged)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--icons', type=Path, required=True, help='Local pinned trapd00r icons directory')
    parser.add_argument('--w2k', type=Path, required=True, help='Local unpacked W2KTheme root')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print(f'Staged {prepare(args.icons, args.w2k, args.output)} private resources; no code executed')
