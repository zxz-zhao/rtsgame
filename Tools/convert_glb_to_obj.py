#!/usr/bin/env python3
"""Small GLB-to-OBJ converter for simple static glTF 2.0 model assets.

This intentionally avoids third-party packages so the Unity project can import
downloaded GLB assets as OBJ/MTL without adding a runtime importer dependency.
It supports the subset used by FreePixel's static low-poly models.
"""

from __future__ import annotations

import base64
import json
import math
import re
import struct
import sys
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


COMPONENT_TYPES = {
    5120: ("b", 1, True),
    5121: ("B", 1, False),
    5122: ("h", 2, True),
    5123: ("H", 2, False),
    5125: ("I", 4, False),
    5126: ("f", 4, False),
}

TYPE_COUNTS = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}


def sanitize(name: str, fallback: str = "asset") -> str:
    name = re.sub(r"[^A-Za-z0-9_.-]+", "_", name.strip())
    return name.strip("._") or fallback


def read_glb(path: Path) -> Tuple[dict, List[bytes]]:
    data = path.read_bytes()
    if len(data) < 12:
        raise ValueError(f"{path} is too small to be a GLB file")

    magic, version, total_length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2:
        raise ValueError(f"{path} is not a glTF 2.0 GLB")
    if total_length > len(data):
        raise ValueError(f"{path} has an invalid GLB length")

    json_chunk: Optional[bytes] = None
    bin_chunks: List[bytes] = []
    offset = 12
    while offset + 8 <= total_length:
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        chunk = data[offset : offset + chunk_length]
        offset += chunk_length
        if chunk_type == 0x4E4F534A:
            json_chunk = chunk
        elif chunk_type == 0x004E4942:
            bin_chunks.append(chunk)

    if json_chunk is None:
        raise ValueError(f"{path} does not contain a JSON chunk")

    gltf = json.loads(json_chunk.rstrip(b" \t\r\n\0").decode("utf-8"))
    buffers = resolve_buffers(path, gltf, bin_chunks)
    return gltf, buffers


def resolve_buffers(path: Path, gltf: dict, bin_chunks: List[bytes]) -> List[bytes]:
    buffers: List[bytes] = []
    bin_index = 0
    for buffer_def in gltf.get("buffers", []):
        uri = buffer_def.get("uri")
        if not uri:
            if bin_index >= len(bin_chunks):
                raise ValueError(f"{path} references a missing binary buffer")
            buffers.append(bin_chunks[bin_index])
            bin_index += 1
        elif uri.startswith("data:"):
            buffers.append(base64.b64decode(uri.split(",", 1)[1]))
        else:
            buffers.append((path.parent / uri).read_bytes())
    return buffers


def accessor_values(gltf: dict, buffers: Sequence[bytes], accessor_index: int) -> List[Tuple[float, ...]]:
    accessor = gltf["accessors"][accessor_index]
    if "sparse" in accessor:
        raise ValueError("Sparse accessors are not supported by this lightweight converter")

    view = gltf["bufferViews"][accessor["bufferView"]]
    buffer_data = buffers[view.get("buffer", 0)]
    component_type = accessor["componentType"]
    fmt_char, component_size, signed = COMPONENT_TYPES[component_type]
    component_count = TYPE_COUNTS[accessor["type"]]
    item_size = component_size * component_count
    stride = view.get("byteStride", item_size)
    base_offset = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    count = accessor["count"]
    fmt = "<" + fmt_char * component_count
    normalized = accessor.get("normalized", False)

    out: List[Tuple[float, ...]] = []
    for i in range(count):
        start = base_offset + i * stride
        values = struct.unpack_from(fmt, buffer_data, start)
        if normalized and component_type != 5126:
            values = tuple(normalize_component(v, component_size, signed) for v in values)
        out.append(tuple(float(v) for v in values))
    return out


def normalize_component(value: int, byte_size: int, signed: bool) -> float:
    bits = byte_size * 8
    if signed:
        max_value = (1 << (bits - 1)) - 1
        return max(-1.0, value / max_value)
    max_value = (1 << bits) - 1
    return value / max_value


def mat_identity() -> List[float]:
    return [
        1.0,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
    ]


def mat_mul(a: Sequence[float], b: Sequence[float]) -> List[float]:
    out = [0.0] * 16
    for row in range(4):
        for col in range(4):
            out[row * 4 + col] = sum(a[row * 4 + k] * b[k * 4 + col] for k in range(4))
    return out


def mat_from_trs(
    translation: Sequence[float],
    rotation: Sequence[float],
    scale: Sequence[float],
) -> List[float]:
    tx, ty, tz = translation
    sx, sy, sz = scale
    x, y, z, w = rotation
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z

    r = [
        1 - 2 * (yy + zz),
        2 * (xy - wz),
        2 * (xz + wy),
        0.0,
        2 * (xy + wz),
        1 - 2 * (xx + zz),
        2 * (yz - wx),
        0.0,
        2 * (xz - wy),
        2 * (yz + wx),
        1 - 2 * (xx + yy),
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
    ]
    s = [
        sx,
        0.0,
        0.0,
        0.0,
        0.0,
        sy,
        0.0,
        0.0,
        0.0,
        0.0,
        sz,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
    ]
    t = mat_identity()
    t[3], t[7], t[11] = tx, ty, tz
    return mat_mul(t, mat_mul(r, s))


def node_matrix(node: dict) -> List[float]:
    if "matrix" in node:
        src = node["matrix"]
        # glTF stores matrices column-major; convert to row-major.
        return [float(src[col * 4 + row]) for row in range(4) for col in range(4)]
    return mat_from_trs(
        node.get("translation", [0.0, 0.0, 0.0]),
        node.get("rotation", [0.0, 0.0, 0.0, 1.0]),
        node.get("scale", [1.0, 1.0, 1.0]),
    )


def transform_point(m: Sequence[float], p: Sequence[float]) -> Tuple[float, float, float]:
    x, y, z = p[:3]
    return (
        m[0] * x + m[1] * y + m[2] * z + m[3],
        m[4] * x + m[5] * y + m[6] * z + m[7],
        m[8] * x + m[9] * y + m[10] * z + m[11],
    )


def transform_normal(m: Sequence[float], n: Sequence[float]) -> Tuple[float, float, float]:
    x, y, z = n[:3]
    nx = m[0] * x + m[1] * y + m[2] * z
    ny = m[4] * x + m[5] * y + m[6] * z
    nz = m[8] * x + m[9] * y + m[10] * z
    length = math.sqrt(nx * nx + ny * ny + nz * nz)
    if length <= 1e-8:
        return (0.0, 1.0, 0.0)
    return (nx / length, ny / length, nz / length)


def scene_mesh_instances(gltf: dict) -> Iterable[Tuple[int, str, List[float]]]:
    nodes = gltf.get("nodes", [])
    scenes = gltf.get("scenes", [])
    scene_index = gltf.get("scene", 0)
    if scenes:
        roots = scenes[scene_index].get("nodes", [])
    else:
        roots = list(range(len(nodes)))

    def walk(node_index: int, parent: Sequence[float]) -> Iterable[Tuple[int, str, List[float]]]:
        node = nodes[node_index]
        world = mat_mul(parent, node_matrix(node))
        name = sanitize(node.get("name", f"node_{node_index}"), f"node_{node_index}")
        if "mesh" in node:
            yield node["mesh"], name, world
        for child in node.get("children", []):
            yield from walk(child, world)

    for root in roots:
        yield from walk(root, mat_identity())


def primitive_triangles(indices: Sequence[int], mode: int) -> Iterable[Tuple[int, int, int]]:
    if mode == 4:
        for i in range(0, len(indices) - 2, 3):
            yield int(indices[i]), int(indices[i + 1]), int(indices[i + 2])
    elif mode == 5:
        for i in range(len(indices) - 2):
            tri = (int(indices[i]), int(indices[i + 1]), int(indices[i + 2]))
            yield tri if i % 2 == 0 else (tri[1], tri[0], tri[2])
    elif mode == 6:
        for i in range(1, len(indices) - 1):
            yield int(indices[0]), int(indices[i]), int(indices[i + 1])


def extract_texture(
    gltf: dict,
    buffers: Sequence[bytes],
    source_index: int,
    textures_dir: Path,
    asset_stem: str,
) -> Optional[str]:
    images = gltf.get("images", [])
    if source_index < 0 or source_index >= len(images):
        return None

    image = images[source_index]
    name = sanitize(image.get("name", f"image_{source_index}"), f"image_{source_index}")
    mime = image.get("mimeType", "")
    ext = ".png" if mime == "image/png" else ".jpg" if mime in ("image/jpeg", "image/jpg") else ""

    data: Optional[bytes] = None
    uri = image.get("uri")
    if uri:
        if uri.startswith("data:"):
            header, payload = uri.split(",", 1)
            ext = ".jpg" if "jpeg" in header else ".png" if "png" in header else ext or ".bin"
            data = base64.b64decode(payload)
        else:
            ext = Path(uri).suffix or ext or ".bin"
            return uri.replace("\\", "/")
    elif "bufferView" in image:
        view = gltf["bufferViews"][image["bufferView"]]
        buffer_data = buffers[view.get("buffer", 0)]
        start = view.get("byteOffset", 0)
        end = start + view["byteLength"]
        data = buffer_data[start:end]

    if data is None:
        return None

    textures_dir.mkdir(parents=True, exist_ok=True)
    filename = sanitize(f"{asset_stem}_{name}") + (ext or ".bin")
    out_path = textures_dir / filename
    out_path.write_bytes(data)
    return f"textures/{filename}"


def material_defs(gltf: dict, buffers: Sequence[bytes], out_dir: Path, asset_stem: str) -> Tuple[List[str], Dict[int, str]]:
    material_lines: List[str] = []
    material_names: Dict[int, str] = {}
    textures_dir = out_dir / "textures"

    materials = gltf.get("materials", [])
    if not materials:
        materials = [{"name": "Default", "pbrMetallicRoughness": {"baseColorFactor": [0.75, 0.75, 0.75, 1]}}]

    for index, material in enumerate(materials):
        name = sanitize(material.get("name", f"mat_{index}"), f"mat_{index}")
        if name in material_names.values():
            name = f"{name}_{index}"
        material_names[index] = name

        pbr = material.get("pbrMetallicRoughness", {})
        color = pbr.get("baseColorFactor", [0.75, 0.75, 0.75, 1.0])
        texture_rel = None
        tex_info = pbr.get("baseColorTexture")
        if tex_info is not None:
            tex_def = gltf.get("textures", [])[tex_info["index"]]
            texture_rel = extract_texture(gltf, buffers, tex_def.get("source", -1), textures_dir, asset_stem)

        material_lines.extend(
            [
                f"newmtl {name}",
                f"Kd {color[0]:.6f} {color[1]:.6f} {color[2]:.6f}",
                "Ka 0.000000 0.000000 0.000000",
                "Ks 0.050000 0.050000 0.050000",
                "Ns 16.000000",
                f"d {color[3] if len(color) > 3 else 1.0:.6f}",
                "illum 2",
            ]
        )
        if texture_rel:
            material_lines.append(f"map_Kd {texture_rel}")
        material_lines.append("")

    return material_lines, material_names


def convert(glb_path: Path, out_dir: Path) -> Path:
    gltf, buffers = read_glb(glb_path)
    if any("KHR_draco_mesh_compression" in prim.get("extensions", {}) for mesh in gltf.get("meshes", []) for prim in mesh.get("primitives", [])):
        raise ValueError(f"{glb_path} uses Draco compression, which this converter does not support")

    out_dir.mkdir(parents=True, exist_ok=True)
    asset_stem = sanitize(glb_path.stem)
    obj_path = out_dir / f"{asset_stem}.obj"
    mtl_path = out_dir / f"{asset_stem}.mtl"

    mtl_lines, material_names = material_defs(gltf, buffers, out_dir, asset_stem)
    mtl_path.write_text("\n".join(mtl_lines), encoding="utf-8")

    lines: List[str] = [
        f"# Converted from {glb_path.name}",
        f"mtllib {mtl_path.name}",
        "",
    ]
    vertex_offset = 1
    uv_offset = 1
    normal_offset = 1

    for mesh_index, node_name, world in scene_mesh_instances(gltf):
        mesh = gltf["meshes"][mesh_index]
        mesh_name = sanitize(mesh.get("name", f"mesh_{mesh_index}"), f"mesh_{mesh_index}")
        for prim_index, primitive in enumerate(mesh.get("primitives", [])):
            attributes = primitive.get("attributes", {})
            if "POSITION" not in attributes:
                continue

            positions = accessor_values(gltf, buffers, attributes["POSITION"])
            normals = accessor_values(gltf, buffers, attributes["NORMAL"]) if "NORMAL" in attributes else []
            uvs = accessor_values(gltf, buffers, attributes["TEXCOORD_0"]) if "TEXCOORD_0" in attributes else []
            if "indices" in primitive:
                raw_indices = accessor_values(gltf, buffers, primitive["indices"])
                indices = [int(v[0]) for v in raw_indices]
            else:
                indices = list(range(len(positions)))

            lines.append(f"o {node_name}_{mesh_name}_{prim_index}")
            material_index = primitive.get("material", 0)
            lines.append(f"usemtl {material_names.get(material_index, material_names.get(0, 'Default'))}")

            for position in positions:
                x, y, z = transform_point(world, position)
                lines.append(f"v {x:.6f} {y:.6f} {z:.6f}")
            for uv in uvs:
                u, v = uv[:2]
                lines.append(f"vt {u:.6f} {v:.6f}")
            for normal in normals:
                nx, ny, nz = transform_normal(world, normal)
                lines.append(f"vn {nx:.6f} {ny:.6f} {nz:.6f}")

            mode = primitive.get("mode", 4)
            for a, b, c in primitive_triangles(indices, mode):
                face_indices = []
                for local in (a, b, c):
                    vi = vertex_offset + local
                    if uvs and normals:
                        face_indices.append(f"{vi}/{uv_offset + local}/{normal_offset + local}")
                    elif uvs:
                        face_indices.append(f"{vi}/{uv_offset + local}")
                    elif normals:
                        face_indices.append(f"{vi}//{normal_offset + local}")
                    else:
                        face_indices.append(str(vi))
                lines.append("f " + " ".join(face_indices))
            lines.append("")

            vertex_offset += len(positions)
            uv_offset += len(uvs)
            normal_offset += len(normals)

    obj_path.write_text("\n".join(lines), encoding="utf-8")
    return obj_path


def main(argv: Sequence[str]) -> int:
    if len(argv) < 3:
        print("Usage: convert_glb_to_obj.py <output-dir> <model.glb> [model.glb ...]", file=sys.stderr)
        return 2

    out_dir = Path(argv[1])
    for arg in argv[2:]:
        obj = convert(Path(arg), out_dir)
        print(obj)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
