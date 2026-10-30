"""Adapt the pinned, installed Open-Shell MIT skin in a separate output file.

Only SKIN text resources change. Original bitmap resources stay unchanged.
Windows resource APIs load the source as data and never execute it.
"""
from pathlib import Path
import argparse,ctypes,hashlib,shutil
SOURCE_SHA='5c1ac7e266a39ceb358e996a480658e8bc1389a73b5d4f3b8d0374f9059e97fc'

def adapt(source,output):
    if hashlib.sha256(source.read_bytes()).hexdigest()!=SOURCE_SHA:raise ValueError('Unsupported Open-Shell XP skin; expected 4.4.198')
    if output.exists():raise FileExistsError(output)
    k=ctypes.WinDLL('kernel32',use_last_error=True)
    k.LoadLibraryExW.argtypes=[ctypes.c_wchar_p,ctypes.c_void_p,ctypes.c_uint];k.LoadLibraryExW.restype=ctypes.c_void_p
    k.FindResourceW.argtypes=[ctypes.c_void_p,ctypes.c_void_p,ctypes.c_wchar_p];k.FindResourceW.restype=ctypes.c_void_p
    k.SizeofResource.argtypes=[ctypes.c_void_p,ctypes.c_void_p]
    k.LoadResource.argtypes=[ctypes.c_void_p,ctypes.c_void_p];k.LoadResource.restype=ctypes.c_void_p
    k.LockResource.argtypes=[ctypes.c_void_p];k.LockResource.restype=ctypes.c_void_p
    k.FreeLibrary.argtypes=[ctypes.c_void_p]
    handle=k.LoadLibraryExW(str(source.resolve()),None,0x22)
    if not handle:raise ctypes.WinError(ctypes.get_last_error())
    configs={}
    try:
        for identifier in [1,2,3]:
            resource=k.FindResourceW(handle,identifier,'SKIN')
            if not resource:raise ctypes.WinError(ctypes.get_last_error())
            data=ctypes.string_at(k.LockResource(k.LoadResource(handle,resource)),k.SizeofResource(handle,resource))
            text=data.decode('utf8').replace('"Segoe UI"','"Tahoma"')
            text=text.replace('Main_font="Tahoma",normal,-10','Main_font="Tahoma",normal,-8').replace('Main_font="Tahoma",normal,-9','Main_font="Tahoma",normal,-8')
            text=text.replace('Submenu_font="Tahoma",normal,-9','Submenu_font="Tahoma",normal,-8').replace('Main_opacity=alpha','Main_opacity=region')
            configs[identifier]=text.encode('utf8')
    finally:k.FreeLibrary(handle)
    output.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,output)
    k.BeginUpdateResourceW.argtypes=[ctypes.c_wchar_p,ctypes.c_bool];k.BeginUpdateResourceW.restype=ctypes.c_void_p
    k.UpdateResourceW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_void_p,ctypes.c_ushort,ctypes.c_void_p,ctypes.c_uint]
    k.EndUpdateResourceW.argtypes=[ctypes.c_void_p,ctypes.c_bool]
    update=k.BeginUpdateResourceW(str(output.resolve()),False)
    if not update:raise ctypes.WinError(ctypes.get_last_error())
    try:
        for identifier,data in configs.items():
            if not k.UpdateResourceW(update,'SKIN',identifier,1033,data,len(data)):raise ctypes.WinError(ctypes.get_last_error())
    except:
        k.EndUpdateResourceW(update,True);raise
    if not k.EndUpdateResourceW(update,False):raise ctypes.WinError(ctypes.get_last_error())
    return hashlib.sha256(output.read_bytes()).hexdigest()

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--source',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    print('Separate MIT-derived XP menu skin SHA-256:',adapt(a.source,a.output))
