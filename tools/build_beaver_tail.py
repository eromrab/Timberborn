"""Build Tidetails beaver models with a larger tail (mesh deform from vanilla)."""
import os
import sys
import zlib

TB = os.path.join(os.environ.get("TEMP", ""), "tb_research")
sys.path.insert(0, TB)

from buildpump import enc_vec, enc_varint, key, ld, raw_fields, read_model_bytes  # noqa: E402
from buildtrees import enc_mesh, enc_prop, parse_main, transform_positions  # noqa: E402

REPO = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(REPO, "Tidetails", "Characters", "Beaver")

TAIL_Z = -0.12
PIVOT = (0.0, 0.10, -0.06)
SCALE = (1.34, 1.2, 1.48)
MESH_NAMES = {"#BeaverAdult.001", "#BeaverChild"}


def deform_tail(n):
    if not n["vcount"] or n["name"] not in MESH_NAMES:
        return

    def fn(p, _i):
        if p[2] >= TAIL_Z:
            return p
        return [
            PIVOT[0] + (p[0] - PIVOT[0]) * SCALE[0],
            PIVOT[1] + (p[1] - PIVOT[1]) * SCALE[1],
            PIVOT[2] + (p[2] - PIVOT[2]) * SCALE[2],
        ]

    transform_positions(n, fn)


def write_model(src_name, out_path):
    data = read_model_bytes(src_name)
    other = b"".join(r for f, wt, v, r in raw_fields(data) if f != 3)
    nodes_out = []
    for f, wt, v, r in raw_fields(data):
        if f != 3:
            continue
        n = parse_main(v)
        deform_tail(n)
        nodes_out.append(n)
    model = other
    for n in nodes_out:
        body = bytearray()
        p = n["parent"]
        if p < 0:
            body += key(1, 0) + enc_varint(p + (1 << 64))
        else:
            body += key(1, 0) + enc_varint(p)
        body += ld(2, n["name"].encode())
        body += ld(3, enc_vec(list(n["pos"])))
        body += ld(4, enc_vec(list(n["rot"])))
        body += ld(5, enc_vec(list(n["scale"])))
        for chunk in n.get("keep", []):
            body += chunk
        if n["meshes"]:
            body += key(6, 0) + enc_varint(n["vcount"])
            for pname, st, dim, vals in n["props"]:
                if len(vals) < n["vcount"]:
                    vals.extend([[0.0] * dim] * (n["vcount"] - len(vals)))
                elif len(vals) > n["vcount"]:
                    del vals[n["vcount"] :]
                body += enc_prop(pname, st, dim, vals)
            for mat, idx in n["meshes"]:
                if idx:
                    body += enc_mesh(mat, idx)
        model += ld(3, bytes(body))
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(zlib.compress(model, 6))
    print("wrote", out_path, "nodes", len(nodes_out))


def main():
    write_model(
        "BeaverAdult.Model",
        os.path.join(OUT_DIR, "BeaverAdult.Tidetails.Model.timbermesh"),
    )
    write_model(
        "BeaverChild.Model",
        os.path.join(OUT_DIR, "BeaverChild.Tidetails.Model.timbermesh"),
    )


if __name__ == "__main__":
    main()
