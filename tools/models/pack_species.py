# Species built on bodies from Quaternius' "Lowpoly Animated Fish" pack (CC0, quaternius.itch.io/lowpoly-animated-fish),
# with FishMon's own features added on top. These are flat-shaded throughout to match the pack's faceted style.
import math
from mathutils import Vector
from lowpoly import imported, hit, paint, revolve, tube, crystal, sphere, disc, eye, fin


def _on(obj, x, z, s, inward=True):
    """The point on the body's left (s=1) or right (s=-1) side at height z, found by shooting a ray in from outside."""
    p, n = hit(obj, (x, s * 2.0, z), (0, -s, 0))
    return p, n


def _top(obj, x, y=0.0):
    p, n = hit(obj, (x, y, 2.0), (0, 0, -1))
    return p, n


def _bottom(obj, x, y=0.0):
    p, n = hit(obj, (x, y, -2.0), (0, 0, 1))
    return p, n


def _eye(m, obj, x, z, r, iris="eye_black", sink=0.35):
    for s in (1, -1):
        p, n = _on(obj, x, z, s)
        if p is None: continue
        eye(m, p - n * r * sink, r, n, iris)


def driftray(m):
    m.flat = True
    obj, lo, hi = imported(m, "Manta ray", 1.9, {"Top": "ray_top", "Bottom": "ray_belly"})
    # dark wingtips and tail
    paint(obj, lambda c, n, col: "ray_dark" if col == "ray_top" and (abs(c.y) > 0.42 or c.x < hi.x - 0.95) else None)
    for x, y in ((0.55, 0.3), (0.42, 0.17), (0.66, 0.14), (0.47, 0.44), (0.32, 0.3)):
        for s in (1, -1):
            p, n = _top(obj, x, s * y)
            if p is not None: disc(m, "spot", p - n * 0.006, 0.035, n, "ray_spot", depth=0.25)
    _eye(m, obj, hi.x - 0.16, 0.015, 0.034)


def tidefang(m):
    # the pack's shark as it is, just bigger and in Tidefang's colours
    m.flat = True
    imported(m, "Shark", 1.2, {"Top": "fang_slate", "Bottom": "fang_belly"})


def frostfin(m):
    m.flat = True
    obj, lo, hi = imported(m, "Fish1", 0.76, {"Top": "frost_blue", "Bottom": "frost_white", "Fins": "frost_crystal"})
    paint(obj, lambda c, n, col: "frost_deep" if col == "frost_blue" and n.z > 0.75 else None)
    def grown(x, y, dx, dy, h, r):
        # rooted in the body right under it (the back is a sharp ridge, so each crystal finds its own surface)
        p, n = _top(obj, x, y)
        if p is None: return
        base = p - Vector((0, 0, r * 0.8))
        crystal(m, "crystal", base, (x + dx, y + dy, p.z + h), r, "frost_crystal", "frost_white")

    for x, h, lean in ((0.13, 0.09, 0.25), (0.05, 0.13, 0.3), (-0.03, 0.12, 0.35), (-0.11, 0.085, 0.4), (-0.18, 0.06, 0.45)):
        grown(x, 0.0, -h * lean, 0.0, h, 0.02)
        grown(x - 0.014, 0.008, -h * lean - 0.03, 0.022, h * 0.55, 0.011)
        grown(x + 0.01, -0.007, -h * lean * 0.5, -0.02, h * 0.45, 0.01)
    _eye(m, obj, hi.x - 0.07, 0.012, 0.018)


def glimmerjel(m):
    # electric, not jelly: glowing stripes, a jagged lightning crest and spark crystals on its back
    m.flat = True
    obj, lo, hi = imported(m, "Fish3", 0.24, {"Body": "jel_cyan", "Stripes": "jel_glow", "Outline": "jel_deep"})
    paint(obj, lambda c, n, col: "jel_deep" if col == "jel_cyan" and n.z > 0.8 else None)
    p, n = _top(obj, 0.02)
    top = p.z if p is not None else hi.z * 0.6
    bolt = [(0.06, 0.0), (0.035, 0.03), (0.05, 0.03), (0.015, 0.065), (0.025, 0.04), (0.0, 0.04), (0.02, 0.0)]
    fin(m, [(u, v) for u, v in bolt], "jel_glow", 0.006, pos=(-0.03, 0, top - 0.008), smooth=0)
    for x, h in ((0.055, 0.02), (-0.045, 0.018), (-0.07, 0.014)):
        q, qn = _top(obj, x)
        if q is not None:
            crystal(m, "spark", q - Vector((0, 0, 0.004)), q + Vector((-0.004, 0, h)), 0.007, "jel_glow", "jel_pale", segments=4)
    for s in (1, -1):
        q, qn = _on(obj, hi.x - 0.07, -0.012, s)
        if q is not None: disc(m, "cheek", q - qn * 0.003, 0.009, qn, "jel_glow")
    _eye(m, obj, hi.x - 0.04, 0.008, 0.016)


def coralit(m):
    m.flat = True
    obj, lo, hi = imported(m, "Fish2", 0.42, {"Body": "coral_pink", "Front": "coral_light", "Fins": "coral_dark"})
    paint(obj, lambda c, n, col: "cream" if col == "coral_pink" and n.z < -0.55 else None)
    p, n = _top(obj, 0.05)
    base = p.z - 0.01 if p is not None else hi.z * 0.5

    def coral(pts, radii, colour):
        tube(m, "coral", pts, radii, colour, segments=6, subsurf=0, colours=[colour] * (len(pts) - 2) + ["coral_light"])

    x0 = 0.05
    coral([(x0, 0, base), (x0 + 0.004, 0, base + 0.05), (x0 + 0.016, 0.004, base + 0.09), (x0 + 0.026, 0.008, base + 0.125)], [0.026, 0.023, 0.019, 0.016], "coral_orange")
    coral([(x0 + 0.004, 0, base + 0.045), (x0 - 0.026, 0.012, base + 0.072), (x0 - 0.042, 0.02, base + 0.1)], [0.017, 0.015, 0.013], "coral_orange")
    coral([(x0 + 0.016, 0.004, base + 0.085), (x0 + 0.048, -0.016, base + 0.1), (x0 + 0.064, -0.026, base + 0.114)], [0.015, 0.013, 0.011], "coral_orange")
    coral([(x0 - 0.05, 0.03, base - 0.01), (x0 - 0.06, 0.04, base + 0.03), (x0 - 0.068, 0.048, base + 0.06)], [0.018, 0.016, 0.013], "reef_purple")
    for s_ in (1, -1):                                  # two smaller clumps leaning out to the sides
        coral([(x0 - 0.02, s_ * 0.012, base - 0.004), (x0 - 0.03, s_ * 0.04, base + 0.035), (x0 - 0.036, s_ * 0.058, base + 0.062)], [0.016, 0.014, 0.012], "reef_purple" if s_ > 0 else "coral_orange")
        coral([(x0 + 0.06, s_ * 0.01, base - 0.012), (x0 + 0.07, s_ * 0.032, base + 0.022), (x0 + 0.074, s_ * 0.044, base + 0.044)], [0.013, 0.011, 0.01], "coral_orange" if s_ > 0 else "reef_purple")
    for (dx, dy, r) in ((0.04, 0.03, 0.017), (-0.03, -0.03, 0.014), (0.09, -0.02, 0.013)):
        sphere(m, "polyp", (x0 + dx, dy, base + 0.006), (r, r, r * 0.8), "reef_purple", segments=8, rings=5)
    _eye(m, obj, hi.x - 0.07, 0.025, 0.026)


def wavelet(m):
    # the pack's dolphin, recoloured
    m.flat = True
    imported(m, "Dolphin", 1.4, {"Top": "wave_top", "Bottom": "wave_belly"})


def hullback(m):
    # the pack's whale, recoloured
    m.flat = True
    imported(m, "Whale", 4.5, {"Top": "hull_top", "Bottom": "hull_belly"})


SPECIES = {
    "driftray": driftray,
    "tidefang": tidefang,
    "frostfin": frostfin,
    "sparkfin": glimmerjel,
    "wavelet": wavelet,
    "hullback": hullback,
    "coralit": coralit,
}
