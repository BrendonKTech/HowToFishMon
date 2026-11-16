# Builds the ocean species' models and renders previews.
#
#   "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P tools/models/make_models.py -- [out_dir] [preview_dir] [only]
#
# Writes <name>.obj (Unity axes, facing +Z like the game's fish, with smooth normals) and one shared palette.png
# into out_dir (default assets/models), and previews into preview_dir. The mod embeds them (src/Sea/Models.cs).
import os, sys, math, struct, zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import lowpoly as lp
from species import SPECIES, COLOURS
import pack_species
SPECIES = {**SPECIES, **pack_species.SPECIES}   # the pack-based versions replace the hand-built ones

lp.COLOURS.update(COLOURS)
lp.PACK = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Fish Pack Animated by Quaternius").replace("\\", "/")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.abspath(argv[0]) if len(argv) > 0 else os.path.join(ROOT, "assets", "models")
PREVIEW = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.join(OUT, "previews")
ONLY = set(argv[2].split(",")) if len(argv) > 2 and argv[2] else None
SMOOTH_ANGLE = math.radians(40)   # edges sharper than this stay sharp
PALETTE_SIZE = 8
os.makedirs(OUT, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)

names = list(COLOURS.keys())
assert len(names) <= PALETTE_SIZE ** 2, "too many colours for the palette"


def cell_uv(colour):
    i = names.index(colour.split(".")[0])
    return ((i % PALETTE_SIZE + 0.5) / PALETTE_SIZE, (i // PALETTE_SIZE + 0.5) / PALETTE_SIZE)


def write_palette(path):
    n = PALETTE_SIZE
    rows = []
    for y in range(n - 1, -1, -1):  # PNG rows go top-down, UV v=0 is the bottom
        row = bytearray([0])
        for x in range(n):
            i = y * n + x
            r, g, b = COLOURS[names[i]] if i < len(names) else (255, 0, 255)
            row += bytes([r, g, b, 255])
        rows.append(bytes(row))
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", n, n, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b""))


def finish(m):
    """Apply every part's modifiers, join into one mesh, triangulate, point faces outwards and smooth by angle."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in m.objects: o.select_set(True)
    bpy.context.view_layer.objects.active = m.objects[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = m.name
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # every closed part faces outwards
    bm.to_mesh(obj.data)
    bm.free()
    if m.flat: bpy.ops.object.shade_flat()
    else: bpy.ops.object.shade_smooth_by_angle(angle=SMOOTH_ANGLE)
    return obj


def write_obj(obj, path):
    # Blender (head +X, left +Y, up +Z) -> the game's fish (head +Z, right +X, up +Y): Unity (-y, z, x).
    # Going from right- to left-handed axes flips every triangle, so the corner order is reversed to match.
    me = obj.data
    me.calc_loop_triangles()
    normals = me.corner_normals
    lines = [f"# {obj.name}, made by tools/models/make_models.py"]
    for v in me.vertices:
        x, y, z = v.co
        lines.append(f"v {-y:.5f} {z:.5f} {x:.5f}")
    uv_index, n_index = {}, {}
    body = []
    for tri in me.loop_triangles:
        colour = me.materials[tri.material_index].name
        if colour not in uv_index:
            uv_index[colour] = len(uv_index) + 1
            u, v = cell_uv(colour)
            lines.append(f"vt {u:.5f} {v:.5f}")
        corners = []
        for k in (0, 2, 1):
            nx, ny, nz = normals[tri.loops[k]].vector
            key = (round(-ny, 4), round(nz, 4), round(nx, 4))
            if key not in n_index:
                n_index[key] = len(n_index) + 1
                lines.append(f"vn {key[0]:.4f} {key[1]:.4f} {key[2]:.4f}")
            corners.append(f"{tri.vertices[k] + 1}/{uv_index[colour]}/{n_index[key]}")
        body.append("f " + " ".join(corners))
    open(path, "w").write("\n".join(lines + body) + "\n")
    return len(me.loop_triangles)


def render(obj, path):
    scene = bpy.context.scene
    xs = [v.co.x for v in obj.data.vertices]; ys = [v.co.y for v in obj.data.vertices]; zs = [v.co.z for v in obj.data.vertices]
    size = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    centre = ((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, (max(zs) + min(zs)) / 2)
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_backface_culling = True   # inside-out faces would show up as holes
    sh.show_object_outline = True
    scene.world = bpy.data.worlds.new("w")
    scene.render.resolution_x = scene.render.resolution_y = 560
    from mathutils import Vector
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size * 1.3
    scene.collection.objects.link(cam)
    scene.camera = cam
    outs = []
    for label, d in (("front", (1.0, -0.9, 0.45)), ("side", (0.0, -1.0, 0.08)), ("back", (-0.9, 0.8, 0.5))):
        d = Vector(d).normalized()
        cam.location = Vector(centre) + d * size * 3
        cam.rotation_euler = (Vector(centre) - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = path.replace(".png", f"_{label}.png")
        bpy.ops.render.render(write_still=True)
        outs.append(scene.render.filepath)
    return outs


write_palette(os.path.join(OUT, "palette.png"))
for key, build in SPECIES.items():
    if ONLY and key not in ONLY: continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m = lp.Model(key)
    build(m)
    obj = finish(m)
    tris = write_obj(obj, os.path.join(OUT, key + ".obj"))
    shots = render(obj, os.path.join(PREVIEW, key + ".png"))
    print(f"MODEL {key}: {tris} tris -> {', '.join(shots)}")
