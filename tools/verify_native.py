"""Validate PE architecture, module entry points and hashes without loading code."""
from pathlib import Path
import json,struct,hashlib
ROOT=Path(__file__).resolve().parents[1]

def pe_info(path):
    b=path.read_bytes()
    def u16(o):return struct.unpack_from('<H',b,o)[0]
    def u32(o):return struct.unpack_from('<I',b,o)[0]
    if b[:2]!=b'MZ':raise ValueError('Not a PE: '+str(path))
    pe=u32(0x3c)
    if b[pe:pe+4]!=b'PE\0\0':raise ValueError('Invalid PE signature')
    opt=pe+24;directories=opt+(112 if u16(opt)==0x20b else 96)
    sections=[]
    for i in range(u16(pe+6)):
        o=opt+u16(pe+20)+40*i
        sections.append((u32(o+12),max(u32(o+8),u32(o+16)),u32(o+20)))
    def off(rva):
        for va,size,raw in sections:
            if va<=rva<va+size:return raw+rva-va
        raise ValueError(f'RVA outside sections: {rva:x}')
    def string(o):return b[o:b.index(0,o)].decode('ascii')
    export_rva=u32(directories);exports=[]
    if export_rva:
        e=off(export_rva);names=off(u32(e+32))
        exports=[string(off(u32(names+4*i))) for i in range(u32(e+24))]
    imports=[];irva=u32(directories+8)
    if irva:
        o=off(irva)
        while any(b[o:o+20]):
            imports.append(string(off(u32(o+12))));o+=20
    return {'machine':hex(u16(pe+4)),'exports':exports,'imports':imports}

def verify():
    catalog=json.loads((ROOT/'content/catalog.json').read_text(encoding='utf-8'));results=[]
    for module in catalog['modules']:
        assert module['status']=='built',module['id']
        for binary in module['binaries']:
            path=ROOT/'content'/binary['path'];info=pe_info(path)
            assert hashlib.sha256(path.read_bytes()).hexdigest()==binary['sha256'],path
            expected='0x8664' if '/64/' in binary['path'] else '0x14c'
            assert info['machine']==expected,(path,info['machine'])
            # Windhawk v1.7.3/mod.cpp looks up the Itanium C++ ABI spelling, not a C export.
            assert '_Z10Wh_ModInitv' in info['exports'],(path,'Missing Wh_ModInit ABI export')
            assert 'InternalWhModPtr' in info['exports'],(path,'Missing Windhawk module binding')
            results.append({'module':module['id'],'file':binary['path'],**info})
    output=ROOT/'build/native-verification.json';output.parent.mkdir(exist_ok=True)
    output.write_text(json.dumps(results,indent=2),encoding='utf-8')
    print(f'PASS {len(results)} native DLLs: SHA-256, PE architecture and Wh_ModInit exports')
    print('Imports:',', '.join(sorted(set(i for r in results for i in r['imports']))))
    return results

if __name__=='__main__':verify()
