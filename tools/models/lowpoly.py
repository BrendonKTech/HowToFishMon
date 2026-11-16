# Building blocks for the ocean species. Runs inside Blender (see make_models.py).
# Axes: X = towards the head, Y = the fish's left, Z = up. Metres.
# Parts are separate Blender objects with modifiers (subdivision for smooth shapes, solidify for fins);
# make_models.py applies them, joins everything and smooths by angle, so curves look round and crystals stay sharp.
import math
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

COLOURS = {}  # name -> (r, g, b), set by species.py


class Model:
    def __init__(self, name):
        self.name = name
        self.objects = []
        self.flat = False   # flat-shade the whole model (for ones built on the faceted Quaternius bodies)


def _linear(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        r, g, b = COLOURS[name]
        mat.diffuse_color = (_linear(r), _linear(g), _linear(b), 1)
    return mat


def _object(m, name, verts, faces, colours, subsurf=0, solidify=0.0):
    """colours: one name for the whole part, or one per face."""
    if isinstance(colours, str): colours = [colours] * len(faces)
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    used = list(dict.fromkeys(colours))
    for c in used: me.materials.append(material(c))
    for poly, c in zip(me.polygons, colours): poly.material_index = used.index(c)
    me.update()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    if solidify:
        mod = obj.modifiers.new("solidify", "SOLIDIFY")
        mod.thickness = solidify
        mod.offset = 0
        mod.use_even_offset = True
    if subsurf:
        mod = obj.modifiers.new("subsurf", "SUBSURF")
        mod.levels = mod.render_levels = subsurf
    m.objects.append(obj)
    return obj


def _frame(direction):
    """Rotation that turns +Z into direction."""
    return Vector((0, 0, 1)).rotation_difference(Vector(direction).normalized()).to_matrix()


PACK = None  # folder of the Quaternius fish pack (CC0), set by make_models.py


def imported(m, file, length, colours, head="-Y"):
    """A body from the Quaternius pack, turned to face +X, scaled to length metres and centred on the origin.
    colours maps the pack's material names to ours. Returns the body's (min, max) corners after fitting."""
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=f"{PACK}/OBJ/{file}.obj")
    obj = next(o for o in bpy.data.objects if o not in before and o.type == "MESH")
    me = obj.data
    me.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    turn = {"-Y": lambda v: (-v.y, v.x, v.z), "+Y": lambda v: (v.y, -v.x, v.z),
            "-X": lambda v: (-v.x, -v.y, v.z), "+X": lambda v: (v.x, v.y, v.z)}[head]
    pts = [Vector(turn(v.co)) for v in me.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    k = length / (hi.x - lo.x)
    centre = (lo + hi) / 2
    for v, p in zip(me.vertices, pts): v.co = (p - centre) * k
    for i, mat in enumerate(me.materials):
        me.materials[i] = material(colours.get(mat.name.split(".")[0], next(iter(colours.values()))))
    me.update()
    m.objects.append(obj)
    return obj, (lo - centre) * k, (hi - centre) * k


def hit(obj, origin, direction):
    """Where a ray from origin first meets obj's surface: (point, normal), or (None, None)."""
    bpy.context.view_layer.update()
    ok, loc, nrm, _ = obj.ray_cast(Vector(origin), Vector(direction).normalized())
    return (loc, nrm) if ok else (None, None)


def paint(obj, rule):
    """Recolour faces: rule(centre, normal, colour) returns a new colour name, or None to leave it."""
    me = obj.data
    for poly in me.polygons:
        current = me.materials[poly.material_index].name
        new = rule(poly.center, poly.normal, current)
        if not new or new == current: continue
        names = [mt.name for mt in me.materials]
        if new not in names:
            me.materials.append(material(new))
            names.append(new)
        poly.material_index = names.index(new)
    me.update()


# ---------------- shapes ----------------

def lathe(m, name, rings, segments, pattern, subsurf=2, squareness=0.0, offset=0.0):
    """Body along X. rings: (x, half_width, half_height, z_centre[, y_centre]) tail to nose.
    pattern(ring, angle) -> colour; angle is the face's direction around the body (pi/2 = top)."""
    rings = [r if len(r) == 5 else (*r, 0.0) for r in rings]
    verts = [(rings[0][0] - 0.004, rings[0][4], rings[0][3])]
    for (x, ry, rz, zc, yc) in rings:
        for k in range(segments):
            a = offset + 2 * math.pi * k / segments
            ca, sa = math.cos(a), math.sin(a)
            if squareness:
                n = max(abs(ca), abs(sa))
                ca, sa = ca + (ca / n - ca) * squareness, sa + (sa / n - sa) * squareness
            verts.append((x, yc + ry * ca, zc + rz * sa))
    last = rings[-1]
    verts.append((last[0] + 0.004, last[4], last[3]))
    nose = len(verts) - 1
    idx = lambda r, k: 1 + r * segments + k % segments
    mid = lambda k: offset + 2 * math.pi * (k + 0.5) / segments
    faces, colours = [], []
    for k in range(segments):
        faces.append((0, idx(0, k + 1), idx(0, k))); colours.append(pattern(-1, mid(k)))
    for r in range(len(rings) - 1):
        for k in range(segments):
            faces.append((idx(r, k), idx(r, k + 1), idx(r + 1, k + 1), idx(r + 1, k))); colours.append(pattern(r, mid(k)))
    for k in range(segments):
        faces.append((idx(len(rings) - 1, k), idx(len(rings) - 1, k + 1), nose)); colours.append(pattern(len(rings) - 1, mid(k)))
    return _object(m, name, verts, faces, colours, subsurf)


def revolve(m, name, centre, profile, segments, pattern, subsurf=2, scallop=None):
    """Solid of revolution around the vertical axis at centre. profile: (height, radius) from the top pole down and
    back in to the axis. scallop: (profile index, amount) makes a wavy rim. pattern(profile_index, angle) -> colour."""
    cx, cy, cz = centre
    verts, rings = [], []
    for i, (h, r) in enumerate(profile):
        if r <= 1e-6:
            rings.append(len(verts)); verts.append((cx, cy, cz + h)); continue
        ring = []
        for k in range(segments):
            a = 2 * math.pi * k / segments
            rr = r * (1 + (scallop[1] if k % 2 else -scallop[1])) if scallop and i == scallop[0] else r
            ring.append(len(verts)); verts.append((cx + rr * math.cos(a), cy + rr * math.sin(a), cz + h))
        rings.append(ring)
    faces, colours = [], []
    for i in range(len(profile) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(segments):
            ang = 2 * math.pi * (k + 0.5) / segments
            k1 = (k + 1) % segments
            if isinstance(a, int) and isinstance(b, int): continue
            if isinstance(a, int): faces.append((a, b[k], b[k1]))
            elif isinstance(b, int): faces.append((a[k], a[k1], b))
            else: faces.append((a[k], a[k1], b[k1], b[k]))
            colours.append(pattern(i, ang))
    return _object(m, name, verts, faces, colours, subsurf)


def tube(m, name, points, radii, colour, segments=6, subsurf=1, colours=None, twist=0.0):
    """A tapered tube through points (horns, arms, tentacles, coral, spikes). A radius of 0 at the end makes a point.
    colours: optional colour per segment between points."""
    pts = [Vector(p) for p in points]
    n = len(pts)
    tangents = []
    for i in range(n):
        t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        tangents.append(t.normalized())
    ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
    normal = tangents[0].cross(ref).normalized()
    verts, ring_idx = [], []
    for i in range(n):
        t = tangents[i]
        normal = (normal - t * normal.dot(t)).normalized()          # carry the frame along the curve
        binormal = t.cross(normal)
        if radii[i] <= 1e-6:
            ring_idx.append(len(verts)); verts.append(pts[i]); continue
        ring = []
        for k in range(segments):
            a = twist + 2 * math.pi * k / segments
            ring.append(len(verts))
            verts.append(pts[i] + radii[i] * (math.cos(a) * normal + math.sin(a) * binormal))
        ring_idx.append(ring)
    faces, cols = [], []
    for i in range(n - 1):
        a, b = ring_idx[i], ring_idx[i + 1]
        c = colours[i] if colours else colour
        for k in range(segments):
            k1 = (k + 1) % segments
            if isinstance(b, int): faces.append((a[k], a[k1], b))
            elif isinstance(a, int): faces.append((a, b[k1], b[k]))
            else: faces.append((a[k], a[k1], b[k1], b[k]))
            cols.append(c)
    for end, sign in ((0, -1), (n - 1, 1)):                            # rounded caps
        ring = ring_idx[end]
        if isinstance(ring, int): continue
        tip = len(verts)
        verts.append(pts[end] + tangents[end] * sign * radii[end] * 0.7)
        for k in range(segments):
            k1 = (k + 1) % segments
            faces.append((ring[k1], ring[k], tip) if sign < 0 else (ring[k], ring[k1], tip))
            cols.append(cols[0] if end == 0 else cols[-1])
    return _object(m, name, verts, faces, cols, subsurf)


def crystal(m, name, base, tip, radius, colour, tip_colour=None, segments=6):
    """Faceted crystal: a prism with a pointed tip. Stays sharp-edged."""
    base, tip = Vector(base), Vector(tip)
    shoulder = base + (tip - base) * 0.72
    return tube(m, name, [base, shoulder, tip], [radius, radius * 0.92, 0], colour, segments, 0,
                colours=[colour, tip_colour or colour])


def sphere(m, name, centre, radii, colour, direction=None, segments=12, rings=8, subsurf=0, pattern=None):
    """Ellipsoid. direction: where its local +Z points (for flattened discs and eyes)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    rot = _frame(direction) if direction else Matrix.Identity(3)
    verts = [Vector(centre) + rot @ Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    cols = colour if pattern is None else [pattern(sum((verts[i] for i in f), Vector()) / len(f)) for f in faces]
    bm.free()
    return _object(m, name, verts, faces, cols, subsurf)


def disc(m, name, centre, radius, direction, colour, depth=0.35):
    """A flat round marking set into a surface (spots, cheeks)."""
    return sphere(m, name, centre, (radius, radius, radius * depth), colour, direction, 10, 6)


def fin(m, outline, colour, thickness=0.006, rot=(0, 0, 0), pos=(0, 0, 0), smooth=2):
    """Flat fin: outline is (u, v) points in the fin's plane (u along X, v up). Corners are rounded off."""
    pts = list(outline)
    for _ in range(smooth):                                            # Chaikin corner cutting
        out = []
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            out += [(a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25), (a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75)]
        pts = out
    mat = Euler(rot, "XYZ").to_matrix()
    verts = [Vector(pos) + mat @ Vector((u, 0, v)) for u, v in pts]
    return _object(m, "fin", verts, [tuple(range(len(verts)))], colour, 0, thickness)


def ribbon(m, name, bottom, top, colour, thickness=0.006):
    """A fin strip between two rows of points (a frill running along a curved back)."""
    verts = [Vector(p) for p in bottom] + [Vector(p) for p in top]
    n = len(bottom)
    faces = [(i, i + 1, n + i + 1, n + i) for i in range(n - 1)]
    return _object(m, name, verts, faces, colour, 0, thickness)


def eye(m, pos, r, direction, iris="eye_black", white="eye_white"):
    """Eye set into the head: white, a big iris and a highlight, all facing direction."""
    d = Vector(direction).normalized()
    sphere(m, "eye", pos, (r, r, r * 0.55), white, d, 12, 7)
    sphere(m, "iris", Vector(pos) + d * r * 0.3, (r * 0.66, r * 0.66, r * 0.32), iris, d, 10, 6)
    up = Vector((0, 0, 1)); fwd = Vector((1, 0, 0))
    sphere(m, "shine", Vector(pos) + d * r * 0.52 + up * r * 0.28 + fwd * r * 0.18, (r * 0.17, r * 0.17, r * 0.08), white, d, 6, 4)
