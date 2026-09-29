"""Convert a .glb (glTF 2.0 binary) to OBJ + MTL + textures + a materials.json sidecar, stdlib + numpy only.
Handles node hierarchies (TRS or matrix), POSITION/NORMAL/TEXCOORD_0, triangle lists, metal-rough and
KHR_materials_pbrSpecularGlossiness materials, emissive textures/factors and KHR_materials_emissive_strength.
Usage: python3 glb_to_obj.py in.glb out_dir name [exclude_prefixes] [center_prefixes]
  exclude_prefixes: comma list of material names to drop (Unity merges an OBJ into one mesh, so parts can't be hidden later)
  center_prefixes:  comma list of materials whose bounds are moved to x=z=0 with the lowest kept vertex at y=0"""
import json, struct, sys, os
import numpy as np

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}

def load(path):
    b = open(path, 'rb').read()
    assert b[:4] == b'glTF'
    off, j, binc = 12, None, b''
    while off < len(b):
        ln, typ = struct.unpack('<I4s', b[off:off + 8])
        chunk = b[off + 8:off + 8 + ln]
        if typ == b'JSON': j = json.loads(chunk)
        elif typ == b'BIN\x00': binc = chunk
        off += 8 + ln
    return j, binc

def accessor(j, binc, i):
    a = j['accessors'][i]
    bv = j['bufferViews'][a['bufferView']]
    dt = np.dtype(CT[a['componentType']])
    n = NC[a['type']]
    start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = bv.get('byteStride', 0) or dt.itemsize * n
    count = a['count']
    raw = np.frombuffer(binc, dtype=np.uint8, count=stride * (count - 1) + dt.itemsize * n, offset=start)
    rows = np.lib.stride_tricks.as_strided(raw, shape=(count, dt.itemsize * n), strides=(stride, 1))
    out = np.ascontiguousarray(rows).view(dt).reshape(count, n)
    if a.get('normalized') and dt.kind in 'iu':
        out = out.astype(np.float32) / np.iinfo(dt).max
    return out

def node_matrix(n):
    if 'matrix' in n:
        return np.array(n['matrix'], dtype=np.float64).reshape(4, 4).T
    t = n.get('translation', [0, 0, 0]); r = n.get('rotation', [0, 0, 0, 1]); s = n.get('scale', [1, 1, 1])
    x, y, z, w = r
    R = np.array([[1 - 2*(y*y + z*z), 2*(x*y - z*w), 2*(x*z + y*w)],
                  [2*(x*y + z*w), 1 - 2*(x*x + z*z), 2*(y*z - x*w)],
                  [2*(x*z - y*w), 2*(y*z + x*w), 1 - 2*(x*x + y*y)]])
    M = np.eye(4); M[:3, :3] = R * np.array(s); M[:3, 3] = t
    return M

def main(path, out, name, exclude="", center="", drop_behind=""):
    exclude = [e for e in exclude.split(",") if e]; center = [c for c in center.split(",") if c]
    j, binc = load(path)
    os.makedirs(out, exist_ok=True)
    # textures
    tex_files = {}
    for ti, t in enumerate(j.get('textures', [])):
        src = t.get('source')
        if src is None: continue
        img = j['images'][src]
        ext = '.png' if img.get('mimeType') == 'image/png' else '.jpg'
        fn = f'{name}_tex{src}{ext}'
        if src not in tex_files.values():
            bv = j['bufferViews'][img['bufferView']]
            data = binc[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']]
            open(os.path.join(out, fn), 'wb').write(data)
        tex_files[ti] = fn
    # materials
    mats = []
    for mi, m in enumerate(j.get('materials', [])):
        pbr = m.get('pbrMetallicRoughness', {})
        sg = m.get('extensions', {}).get('KHR_materials_pbrSpecularGlossiness')
        base_tex = pbr.get('baseColorTexture', {}).get('index')
        color = pbr.get('baseColorFactor', [1, 1, 1, 1])
        smooth = 1 - pbr.get('roughnessFactor', 1.0) if pbr else 0.3
        metal = pbr.get('metallicFactor', 1.0 if base_tex is None and 'metallicFactor' in pbr else 0.0) if pbr else 0.0
        if sg:
            base_tex = sg.get('diffuseTexture', {}).get('index', base_tex)
            color = sg.get('diffuseFactor', color)
            smooth = sg.get('glossinessFactor', 0.3)
            metal = 0.0
        em = m.get('emissiveFactor', [0, 0, 0])
        strength = m.get('extensions', {}).get('KHR_materials_emissive_strength', {}).get('emissiveStrength', 1.0)
        mats.append({
            'name': f"{m.get('name', 'mat')}_{mi}", 'color': color, 'baseTex': tex_files.get(base_tex),
            'emissive': [c * strength for c in em], 'emissiveTex': tex_files.get(m.get('emissiveTexture', {}).get('index')),
            'alpha': m.get('alphaMode', 'OPAQUE'), 'cutoff': m.get('alphaCutoff', 0.5), 'doubleSided': m.get('doubleSided', False),
            'smoothness': float(np.clip(smooth, 0, 1)), 'metallic': float(np.clip(metal, 0, 1))})
    json.dump({'materials': mats}, open(os.path.join(out, f'{name}_materials.json'), 'w'), indent=1)
    with open(os.path.join(out, f'{name}.mtl'), 'w') as f:
        for m in mats:
            c = m['color']
            f.write(f"newmtl {m['name']}\nKd {c[0]:.4f} {c[1]:.4f} {c[2]:.4f}\n")
            if m['baseTex']: f.write(f"map_Kd {m['baseTex']}\n")
            f.write('\n')
    # geometry
    lines = [f'mtllib {name}.mtl\n']
    vo = 1
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    tris = 0
    parts = []
    scene = j['scenes'][j.get('scene', 0)]
    stack = [(ni, np.eye(4)) for ni in scene['nodes']]
    while stack:
        ni, parent = stack.pop()
        n = j['nodes'][ni]
        M = parent @ node_matrix(n)
        for c in n.get('children', []): stack.append((c, M))
        if 'mesh' not in n: continue
        mesh = j['meshes'][n['mesh']]
        N = np.linalg.inv(M[:3, :3]).T
        for pi, p in enumerate(mesh['primitives']):
            if p.get('mode', 4) != 4: continue
            pos = accessor(j, binc, p['attributes']['POSITION']).astype(np.float64)
            pos = (M[:3, :3] @ pos.T).T + M[:3, 3]
            nrm = accessor(j, binc, p['attributes']['NORMAL']).astype(np.float64) if 'NORMAL' in p['attributes'] else np.zeros_like(pos)
            nrm = (N @ nrm.T).T
            ln = np.linalg.norm(nrm, axis=1, keepdims=True); ln[ln == 0] = 1; nrm /= ln
            uv = accessor(j, binc, p['attributes']['TEXCOORD_0']).astype(np.float64) if 'TEXCOORD_0' in p['attributes'] else np.zeros((len(pos), 2))
            idx = accessor(j, binc, p['indices']).reshape(-1) if 'indices' in p else np.arange(len(pos))
            if np.linalg.det(M[:3, :3]) < 0: idx = idx.reshape(-1, 3)[:, ::-1].reshape(-1)
            lo = np.minimum(lo, pos.min(0)); hi = np.maximum(hi, pos.max(0))
            mat = mats[p['material']]['name'] if 'material' in p else 'default'
            if any(mat.startswith(e) for e in exclude): continue
            parts.append((f"{mesh.get('name', 'mesh')}_{ni}_{pi}", mat, pos, uv, nrm, idx))
            continue
            lines.append(f"o {mesh.get('name', 'mesh')}_{ni}_{pi}\nusemtl {mat}\n")
            lines.extend(f'v {a:.5f} {b:.5f} {c:.5f}\n' for a, b, c in pos)
            lines.extend(f'vt {a:.5f} {1 - b:.5f}\n' for a, b in uv)
            lines.extend(f'vn {a:.4f} {b:.4f} {c:.4f}\n' for a, b, c in nrm)
            t = idx.reshape(-1, 3) + vo
            lines.extend(f'f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n' for a, b, c in t)
            tris += len(t); vo += len(pos)
    allpos = np.concatenate([p[2] for p in parts])
    ref = [p[2] for p in parts if any(p[1].startswith(c) for c in center)] or [allpos]
    ref = np.concatenate(ref)
    if center and drop_behind:   # decoration standing behind the centred building would poke into whatever is built behind it
        back = ref[:, 2].min()
        parts = [pt for pt in parts if any(pt[1].startswith(c) for c in center) or pt[2][:, 2].mean() >= back - float(drop_behind)]
        allpos = np.concatenate([pt[2] for pt in parts])
    shift = np.array([-(ref[:, 0].min() + ref[:, 0].max()) / 2, -allpos[:, 1].min(), -(ref[:, 2].min() + ref[:, 2].max()) / 2]) if center else np.zeros(3)
    footprint = (ref.max(0) - ref.min(0)).round(3).tolist()
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    for oname, mat, pos, uv, nrm, idx in parts:
        pos = pos + shift
        lo = np.minimum(lo, pos.min(0)); hi = np.maximum(hi, pos.max(0))
        lines.append(f"o {oname}\nusemtl {mat}\n")
        lines.extend(f'v {a:.5f} {b:.5f} {c:.5f}\n' for a, b, c in pos)
        lines.extend(f'vt {a:.5f} {1 - b:.5f}\n' for a, b in uv)
        lines.extend(f'vn {a:.4f} {b:.4f} {c:.4f}\n' for a, b, c in nrm)
        t = idx.reshape(-1, 3) + vo
        lines.extend(f'f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n' for a, b, c in t)
        tris += len(t); vo += len(pos)
    open(os.path.join(out, f'{name}.obj'), 'w').writelines(lines)
    meta = json.load(open(os.path.join(out, f'{name}_materials.json')))
    meta['footprint'] = footprint
    meta['bounds'] = {'min': lo.round(3).tolist(), 'max': hi.round(3).tolist()}
    json.dump(meta, open(os.path.join(out, f'{name}_materials.json'), 'w'), indent=1)
    print(name, 'tris', tris, 'verts', vo - 1, 'bounds', lo.round(2), hi.round(2), 'size', (hi - lo).round(2), 'mats', len(mats), 'textures', len(set(tex_files.values())))

if __name__ == '__main__':
    main(*sys.argv[1:7])
