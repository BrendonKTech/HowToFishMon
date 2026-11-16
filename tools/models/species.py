# The ocean species' shapes (all original designs). Each builder gets an empty lowpoly.Model and adds parts.
# Blender axes: X = towards the head, Y = the fish's left, Z = up. Sizes in metres, close to each template fish.
import math
from mathutils import Vector
from lowpoly import lathe, revolve, tube, crystal, sphere, disc, fin, ribbon, eye

COLOURS = {
    "eye_black": (28, 26, 38), "eye_white": (240, 240, 235), "mouth": (95, 45, 55),
    # Coralit
    "coral_pink": (226, 116, 122), "coral_dark": (184, 78, 92), "coral_orange": (238, 140, 78),
    "coral_light": (250, 200, 165), "cream": (240, 226, 198), "reef_purple": (158, 92, 160),
    # Glimmerjel
    "jel_cyan": (104, 200, 218), "jel_deep": (56, 132, 186), "jel_glow": (240, 218, 96), "jel_pale": (206, 238, 244),
    # Inkling
    "ink_violet": (118, 82, 168), "ink_dark": (72, 48, 112), "ink_pink": (226, 132, 190), "ink_belly": (218, 200, 232),
    # Urchip
    "urch_body": (122, 64, 138), "urch_belly": (160, 110, 170), "urch_spike": (78, 36, 98), "urch_tip": (214, 122, 222),
    # Driftray
    "ray_top": (92, 160, 128), "ray_dark": (54, 108, 90), "ray_belly": (228, 238, 230), "ray_spot": (196, 228, 168),
    # Tidefang
    "fang_slate": (62, 74, 96), "fang_dark": (38, 44, 58), "fang_belly": (196, 204, 214), "fang_stripe": (214, 78, 68), "fang_tooth": (244, 242, 232),
    # Frostfin
    "frost_blue": (118, 172, 216), "frost_deep": (70, 116, 176), "frost_white": (228, 240, 248), "frost_crystal": (168, 222, 244),
    # Leviathorn
    # Bonefin (ghost), Cindergill (fire), Kelpling (grass), Shellmite (bug)
    "bone_white": (226, 220, 200), "bone_shadow": (160, 152, 136), "ghost_glow": (150, 255, 220), "ghost_wisp": (120, 200, 210),
    "cinder_black": (48, 42, 44), "cinder_rock": (92, 80, 78), "lava_orange": (246, 120, 40), "lava_yellow": (255, 206, 80),
    "kelp_green": (78, 138, 64), "kelp_dark": (44, 88, 46), "kelp_light": (160, 196, 88),
    "mite_shell": (150, 132, 170), "mite_dark": (98, 84, 120), "mite_belly": (206, 194, 214), "mite_leg": (120, 104, 140),
    # Wavelet (dolphin) and Hullback (whale)
    "wave_top": (120, 196, 206), "wave_belly": (240, 196, 210),
    "hull_top": (52, 66, 98), "hull_belly": (214, 190, 146),
    "levi_blue": (46, 92, 168), "levi_deep": (28, 56, 112), "levi_gold": (226, 184, 88), "levi_horn": (236, 230, 210), "levi_fin": (86, 186, 206),
}


def two_tone(top, belly, cut=-0.3, extra=None):
    """Body colouring: belly below the cut, top above. extra(ring, sin, cos) can override."""
    def pattern(ring, a):
        s, c = math.sin(a), math.cos(a)
        if extra:
            o = extra(ring, s, c)
            if o: return o
        return belly if s < cut else top
    return pattern


def side(direction, s):
    """Mirror a direction / position to the fish's left (s = 1) or right (s = -1) side."""
    return Vector((direction[0], direction[1] * s, direction[2]))


def coralit(m):
    # chunky boxfish carrying a little reef (~0.42 m)
    rings = [(-0.19, 0.028, 0.034, 0.0), (-0.15, 0.055, 0.066, 0.0), (-0.08, 0.094, 0.104, 0.0), (0.02, 0.108, 0.118, 0.004),
             (0.10, 0.098, 0.104, 0.0), (0.16, 0.07, 0.074, -0.008), (0.195, 0.034, 0.038, -0.018)]
    lathe(m, "body", rings, 8, two_tone("coral_pink", "cream", -0.35,
          lambda r, s, c: "coral_dark" if r == 1 and s > -0.35 else None), squareness=0.45, offset=math.pi / 8)

    fin(m, [(0, 0.03), (-0.05, 0.075), (-0.1, 0.07), (-0.085, 0), (-0.1, -0.07), (-0.05, -0.075), (0, -0.03)],
        "coral_dark", 0.008, pos=(-0.17, 0, 0))
    fin(m, [(0.03, 0), (-0.02, 0.035), (-0.06, 0.03), (-0.05, 0)], "coral_dark", 0.006, pos=(-0.1, 0, -0.085))
    for s in (1, -1):
        fin(m, [(0, 0.015), (-0.045, 0.035), (-0.06, 0.005), (-0.035, -0.015)], "coral_dark", 0.006,
            rot=(s * 1.35, 0, s * -0.3), pos=(0.06, s * 0.1, -0.03))
        eye(m, side((0.12, 0.072, 0.04), s), 0.03, side((0.35, 1, 0.25), s))
        disc(m, "cheek", side((0.115, 0.088, -0.022), s), 0.014, side((0.25, 1, -0.2), s), "coral_light")
    sphere(m, "mouth", (0.196, 0, -0.024), (0.01, 0.017, 0.009), "mouth")

    # the reef: chunky branching coral with pale tips, and two soft polyps
    def coral(pts, radii, colour):
        tube(m, "coral", pts, radii, colour, segments=7, colours=[colour] * (len(pts) - 2) + ["coral_light"])
    coral([(0.0, 0, 0.1), (0.004, 0, 0.15), (0.016, 0.004, 0.19), (0.026, 0.008, 0.226)], [0.028, 0.025, 0.021, 0.018], "coral_orange")
    coral([(0.004, 0, 0.145), (-0.026, 0.012, 0.172), (-0.042, 0.02, 0.2)], [0.019, 0.016, 0.014], "coral_orange")
    coral([(0.016, 0.004, 0.185), (0.048, -0.016, 0.2), (0.064, -0.026, 0.214)], [0.017, 0.014, 0.012], "coral_orange")
    coral([(-0.07, 0.04, 0.085), (-0.08, 0.05, 0.125), (-0.088, 0.058, 0.155)], [0.021, 0.018, 0.015], "reef_purple")
    coral([(-0.08, 0.05, 0.12), (-0.056, 0.07, 0.142), (-0.046, 0.08, 0.158)], [0.014, 0.012, 0.011], "reef_purple")
    coral([(-0.035, -0.05, 0.095), (-0.04, -0.062, 0.128), (-0.05, -0.07, 0.148)], [0.018, 0.015, 0.013], "coral_orange")
    for p, r in (((0.07, 0.035, 0.105), 0.02), ((-0.11, -0.025, 0.07), 0.017), ((0.05, -0.045, 0.105), 0.014)):
        sphere(m, "polyp", p, (r, r, r * 0.8), "reef_purple", segments=10, rings=6, subsurf=1)


def glimmerjel(m):
    # small round fish under a scalloped jelly bell with glowing spots, trailing tentacles (~0.25 m)
    rings = [(-0.095, 0.014, 0.017, 0.0), (-0.07, 0.034, 0.04, 0.0), (-0.03, 0.05, 0.055, 0.0), (0.02, 0.056, 0.058, 0.0),
             (0.06, 0.046, 0.048, -0.003), (0.088, 0.026, 0.027, -0.008), (0.1, 0.01, 0.01, -0.01)]
    lathe(m, "body", rings, 8, two_tone("jel_cyan", "jel_pale", -0.3))
    bell = [(0.06, 0), (0.056, 0.032), (0.044, 0.056), (0.026, 0.07), (0.008, 0.076), (-0.006, 0.072), (-0.002, 0.058), (0.012, 0.04), (0.02, 0)]
    revolve(m, "bell", (-0.004, 0, 0.032), bell, 12, lambda i, a: "jel_pale" if i in (5, 6) else "jel_deep", subsurf=1, scallop=(5, 0.09))
    for a in (0.0, 1.1, -1.1, 2.3, -2.3):
        d = Vector((math.cos(a), math.sin(a), 0.75)).normalized()
        disc(m, "glow", Vector((-0.004, 0, 0.032)) + Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0.05)), 0.011, d, "jel_glow")
    for dy in (-0.032, -0.011, 0.011, 0.032):
        tube(m, "tentacle", [(-0.03, dy, -0.012), (-0.08, dy * 1.2, -0.035), (-0.13, dy * 1.35, -0.028), (-0.17, dy * 1.48, -0.042), (-0.185, dy * 1.52, -0.047)],
             [0.008, 0.007, 0.005, 0.004, 0.0035], "jel_deep", segments=5, colours=["jel_deep", "jel_deep", "jel_deep", "jel_glow"])
    fin(m, [(0, 0.014), (-0.04, 0.04), (-0.055, 0.03), (-0.045, 0), (-0.055, -0.03), (-0.04, -0.04), (0, -0.014)], "jel_deep", 0.005, pos=(-0.09, 0, 0))
    for s in (1, -1):
        eye(m, side((0.058, 0.04, 0.008), s), 0.019, side((0.45, 1, 0.1), s))
        fin(m, [(0, 0.01), (-0.03, 0.022), (-0.04, 0.0), (-0.02, -0.01)], "jel_deep", 0.004, rot=(s * 1.35, 0, s * -0.3), pos=(0.03, s * 0.052, -0.02))
    sphere(m, "mouth", (0.099, 0, -0.012), (0.006, 0.011, 0.005), "mouth")


def inkling(m):
    # little squid: long mantle with a diamond fin, big-eyed head, curling arms and two club tentacles (~0.8 m)
    rings = [(-0.42, 0.014, 0.014, 0.02), (-0.37, 0.046, 0.046, 0.02), (-0.27, 0.085, 0.09, 0.02), (-0.13, 0.108, 0.114, 0.015),
             (-0.02, 0.104, 0.108, 0.01), (0.03, 0.074, 0.078, 0.0), (0.08, 0.094, 0.088, 0.0), (0.14, 0.084, 0.074, -0.005), (0.175, 0.05, 0.044, -0.01)]
    lathe(m, "body", rings, 8, two_tone("ink_violet", "ink_belly", -0.3,
          lambda r, s, c: "ink_dark" if r in (2, 3) and s > 0.6 else None))
    fin(m, [(0.06, 0), (-0.04, 0.13), (-0.12, 0.03), (-0.12, -0.03), (-0.04, -0.13)], "ink_dark", 0.008, rot=(math.pi / 2, 0, 0), pos=(-0.33, 0, 0.02))
    for s in (1, -1):
        eye(m, side((0.1, 0.082, 0.028), s), 0.042, side((0.25, 1, 0.15), s))
        disc(m, "spot", side((-0.2, 0.06, 0.1), s), 0.018, side((0, 0.5, 1), s), "ink_pink")
        disc(m, "spot", side((-0.08, 0.075, 0.095), s), 0.013, side((0, 0.6, 1), s), "ink_pink")
    for i in range(8):
        a = (i + 0.5) * math.pi / 4
        y, z = math.cos(a) * 0.034, math.sin(a) * 0.028 - 0.01
        curl = 0.02 if z > -0.01 else -0.02
        tube(m, "arm", [(0.15, y, z), (0.21, y * 1.5, z * 1.4), (0.27, y * 1.8, z * 1.6 + curl * 0.5), (0.31, y * 1.9, z * 1.7 + curl)],
             [0.017, 0.013, 0.009, 0.005], "ink_violet", segments=6, colours=["ink_violet", "ink_violet", "ink_pink"])
    for s in (1, -1):
        tube(m, "tentacle", [(0.15, s * 0.02, -0.025), (0.24, s * 0.05, -0.05), (0.34, s * 0.06, -0.06), (0.4, s * 0.055, -0.055)],
             [0.011, 0.009, 0.007, 0.006], "ink_dark", segments=6)
        sphere(m, "club", (0.415, s * 0.055, -0.055), (0.022, 0.014, 0.012), "ink_pink", segments=10, rings=6, subsurf=1)
    crystal(m, "gem", (0.118, 0, 0.06), (0.125, 0, 0.1), 0.016, "ink_pink", segments=4)


def urchip(m):
    # grumpy round urchin with glowing spike tips and a stubby tail (~0.3 m with spikes)
    sphere(m, "body", (0, 0, 0), (0.088, 0.086, 0.082), "urch_body", segments=12, rings=8, subsurf=1,
           pattern=lambda c: "urch_belly" if c.z < -0.045 else "urch_body")
    golden = math.pi * (3 - math.sqrt(5))
    n = 30
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(1 - z * z)
        d = Vector((math.cos(golden * i) * r, math.sin(golden * i) * r, z))
        if (d.x > 0.45 and abs(d.z) < 0.6) or d.z < -0.8: continue      # keep the face and underside clear
        tube(m, "spike", [d * 0.07, d * 0.12, d * 0.155], [0.016, 0.008, 0], "urch_spike", segments=5, subsurf=0,
             colours=["urch_spike", "urch_tip"])
    for s in (1, -1):
        eye(m, side((0.07, 0.036, 0.016), s), 0.021, side((1, 0.6, 0.1), s))
        tube(m, "brow", [side((0.075, 0.06, 0.048), s), side((0.086, 0.018, 0.036), s)], [0.006, 0.006], "urch_spike", segments=5)
    tube(m, "frown", [(0.083, -0.02, -0.03), (0.088, 0, -0.024), (0.083, 0.02, -0.03)], [0.004, 0.004, 0.004], "mouth", segments=5)
    fin(m, [(0, 0.014), (-0.035, 0.035), (-0.045, 0.025), (-0.035, 0), (-0.045, -0.025), (-0.035, -0.035), (0, -0.014)], "urch_spike", 0.006, pos=(-0.08, 0, 0))


def driftray(m):
    # gliding ray: soft wide wings with a dark rim, spotted back, curled head lobes, long whip tail (~1.3 m across)
    rings = [(-0.3, 0.03, 0.024, 0.0), (-0.24, 0.2, 0.038, 0.0), (-0.14, 0.44, 0.05, 0.0), (-0.04, 0.6, 0.055, 0.005),
             (0.06, 0.48, 0.055, 0.005), (0.15, 0.26, 0.05, 0.0), (0.23, 0.12, 0.042, 0.0), (0.27, 0.05, 0.028, -0.004)]
    lathe(m, "body", rings, 12, two_tone("ray_top", "ray_belly", -0.25,
          lambda r, s, c: "ray_dark" if abs(c) > 0.93 and r in (1, 2, 3, 4) else None))
    tube(m, "tail", [(-0.28, 0, 0), (-0.5, 0, 0.012), (-0.75, 0, 0.03), (-0.98, 0, 0.05)], [0.022, 0.012, 0.007, 0.002], "ray_dark", segments=6)
    fin(m, [(0.02, 0), (-0.03, 0.05), (-0.08, 0.04), (-0.07, 0)], "ray_dark", 0.006, pos=(-0.33, 0, 0.012))
    for s in (1, -1):
        tube(m, "lobe", [side((0.21, 0.1, 0.0), s), side((0.29, 0.13, -0.004), s), side((0.35, 0.11, -0.014), s)], [0.028, 0.02, 0.012], "ray_dark", segments=6)
        eye(m, side((0.2, 0.1, 0.038), s), 0.026, side((0.3, 0.8, 0.5), s))
        for x, y in ((-0.06, 0.3), (0.04, 0.17), (-0.15, 0.18), (-0.1, 0.42)):
            ry = next(r[1] for r in rings if r[0] >= x)
            z = 0.005 + 0.055 * math.sqrt(max(0.0, 1 - (y / ry) ** 2)) - 0.004
            disc(m, "spot", side((x, y, z), s), 0.03, (0, 0, 1), "ray_spot", depth=0.25)


def tidefang(m):
    # sleek hunter: dark back, red stripe, a jutting jaw with fangs, a swept dorsal fin and forked tail (~0.65 m)
    rings = [(-0.29, 0.015, 0.024, 0.0), (-0.24, 0.035, 0.054, 0.0), (-0.12, 0.064, 0.094, 0.01), (0.04, 0.074, 0.104, 0.012),
             (0.16, 0.064, 0.084, 0.005), (0.24, 0.045, 0.055, -0.004), (0.29, 0.02, 0.024, -0.01)]
    lathe(m, "body", rings, 8, two_tone("fang_slate", "fang_belly", -0.3,
          lambda r, s, c: "fang_stripe" if r == 2 and s >= -0.3 else ("fang_dark" if s > 0.85 else None)))
    fin(m, [(0, 0.02), (-0.07, 0.09), (-0.13, 0.13), (-0.09, 0.03), (-0.075, 0), (-0.09, -0.03), (-0.13, -0.13), (-0.07, -0.09), (0, -0.02)],
        "fang_dark", 0.008, pos=(-0.275, 0, 0))
    fin(m, [(0.08, 0), (0.0, 0.05), (-0.07, 0.12), (-0.09, 0.105), (-0.1, 0)], "fang_dark", 0.007, pos=(0.0, 0, 0.09))
    fin(m, [(0.03, 0), (-0.03, -0.045), (-0.07, -0.04), (-0.06, 0)], "fang_dark", 0.006, pos=(-0.12, 0, -0.07))
    tube(m, "jaw", [(0.12, 0, -0.05), (0.22, 0, -0.045), (0.29, 0, -0.03)], [0.04, 0.034, 0.018], "fang_belly", segments=8)
    for y, x in ((-0.026, 0.262), (-0.01, 0.282), (0.01, 0.282), (0.026, 0.262)):
        tube(m, "fang", [(x, y, -0.03), (x + 0.004, y, -0.012), (x - 0.003, y, 0.004)], [0.006, 0.004, 0], "fang_tooth", segments=5, subsurf=0)
    for s in (1, -1):
        eye(m, side((0.2, 0.054, 0.034), s), 0.022, side((0.3, 1, 0.2), s), iris="fang_stripe")
        tube(m, "brow", [side((0.17, 0.06, 0.058), s), side((0.225, 0.048, 0.05), s)], [0.008, 0.006], "fang_dark", segments=5)
        fin(m, [(0, 0.012), (-0.07, 0.03), (-0.09, 0.0), (-0.05, -0.012)], "fang_dark", 0.006, rot=(s * 1.25, 0, s * -0.25), pos=(0.1, s * 0.07, -0.04))


def frostfin(m):
    # slim icy fish with clusters of faceted crystals along its back (~0.75 m)
    rings = [(-0.33, 0.01, 0.018, 0.0), (-0.27, 0.025, 0.04, 0.0), (-0.14, 0.05, 0.07, 0.0), (0.04, 0.058, 0.078, 0.0),
             (0.19, 0.045, 0.06, -0.004), (0.28, 0.025, 0.032, -0.01), (0.33, 0.008, 0.01, -0.014)]
    lathe(m, "body", rings, 8, two_tone("frost_blue", "frost_white", -0.3, lambda r, s, c: "frost_deep" if s > 0.6 else None))
    fin(m, [(0, 0.015), (-0.07, 0.09), (-0.12, 0.12), (-0.08, 0.02), (-0.065, 0), (-0.08, -0.02), (-0.12, -0.12), (-0.07, -0.09), (0, -0.015)],
        "frost_white", 0.007, pos=(-0.315, 0, 0))
    for x, h, lean in ((0.12, 0.1, 0.25), (0.03, 0.15, 0.3), (-0.06, 0.13, 0.35), (-0.14, 0.09, 0.4), (-0.21, 0.06, 0.45)):
        z = 0.068 if x > -0.15 else 0.045
        crystal(m, "crystal", (x, 0, z - 0.01), (x - h * lean, 0, z + h), 0.022, "frost_crystal", "frost_white")
        crystal(m, "crystal", (x - 0.015, 0.012, z - 0.012), (x - h * lean - 0.03, 0.035, z + h * 0.55), 0.012, "frost_crystal", "frost_white")
        crystal(m, "crystal", (x + 0.01, -0.01, z - 0.012), (x - h * lean * 0.5, -0.03, z + h * 0.45), 0.011, "frost_crystal", "frost_white")
    for s in (1, -1):
        eye(m, side((0.24, 0.036, 0.018), s), 0.02, side((0.35, 1, 0.15), s))
        fin(m, [(0, 0.012), (-0.07, 0.03), (-0.1, 0.0), (-0.05, -0.012)], "frost_crystal", 0.006, rot=(s * 1.25, 0, s * -0.25), pos=(0.15, s * 0.05, -0.03))
    sphere(m, "mouth", (0.332, 0, -0.016), (0.006, 0.009, 0.004), "mouth")


def leviathorn(m):
    # legendary sea serpent: long S-curved body, gold belly, a frill down its back, swept horns and whiskers (~2 m)
    rings = []
    for i in range(13):
        t = i / 12
        r = 0.04 + 0.1 * math.sin(math.pi * min(1.0, t * 1.1)) ** 0.6
        rings.append((-0.95 + t * 1.7, r, r * 1.05, 0.0, 0.08 * math.sin(t * math.pi * 2.2)))
    hy = rings[-1][4]
    rings += [(0.8, 0.075, 0.07, 0.0, hy), (0.87, 0.092, 0.08, 0.004, hy), (0.94, 0.07, 0.056, -0.006, hy), (0.99, 0.035, 0.028, -0.016, hy)]
    lathe(m, "body", rings, 8, two_tone("levi_blue", "levi_gold", -0.35, lambda r, s, c: "levi_deep" if s > 0.85 else None))
    spine = rings[1:13]
    bottom = [(x, y, rz * 0.85) for (x, ry, rz, zc, y) in spine]
    top = [(x - 0.03, y, rz + (0.07 if i % 2 == 0 else 0.045) * (1 - abs(i - 6) / 9)) for i, (x, ry, rz, zc, y) in enumerate(spine)]
    ribbon(m, "frill", bottom, top, "levi_fin", 0.008)
    fin(m, [(0, 0.03), (-0.1, 0.11), (-0.17, 0.12), (-0.12, 0.02), (-0.1, 0), (-0.12, -0.02), (-0.17, -0.12), (-0.1, -0.11), (0, -0.03)],
        "levi_fin", 0.008, pos=(-0.93, rings[0][4], 0))
    for s in (1, -1):
        tube(m, "horn", [(0.86, hy + s * 0.045, 0.06), (0.8, hy + s * 0.085, 0.12), (0.71, hy + s * 0.105, 0.16), (0.63, hy + s * 0.1, 0.175)],
             [0.022, 0.016, 0.01, 0], "levi_horn", segments=6)
        tube(m, "whisker", [(0.97, hy + s * 0.028, -0.02), (1.03, hy + s * 0.075, -0.04), (1.02, hy + s * 0.13, -0.08), (0.96, hy + s * 0.17, -0.11)],
             [0.007, 0.005, 0.004, 0.002], "levi_fin", segments=5)
        eye(m, (0.89, hy + s * 0.064, 0.036), 0.027, side((0.35, 1, 0.3), s), iris="levi_gold")
        fin(m, [(0, 0.02), (-0.1, 0.07), (-0.15, 0.03), (-0.12, -0.02)], "levi_fin", 0.007, rot=(s * 1.3, 0, s * -0.2), pos=(0.74, hy + s * 0.07, -0.01))
    tube(m, "mouth", [(0.94, hy - 0.035, -0.032), (0.985, hy, -0.03), (0.94, hy + 0.035, -0.032)], [0.004, 0.004, 0.004], "mouth", segments=5)


def bonefin(m):
    # a fish skeleton that never noticed it died: skull with glowing sockets, spine, ribs and a ghostly fin (~0.6 m)
    m.flat = True
    sphere(m, "skull", (0.2, 0, 0.012), (0.075, 0.05, 0.06), "bone_white", segments=8, rings=6)
    sphere(m, "snout", (0.265, 0, 0.0), (0.04, 0.03, 0.03), "bone_white", segments=8, rings=5)
    tube(m, "jaw", [(0.14, 0, -0.04), (0.22, 0, -0.045), (0.285, 0, -0.03)], [0.017, 0.014, 0.008], "bone_shadow", segments=6, subsurf=0)
    for s in (1, -1):
        disc(m, "socket", side((0.215, 0.042, 0.022), s), 0.022, side((0.4, 1, 0.2), s), "eye_black", depth=0.5)
        sphere(m, "glow", side((0.222, 0.046, 0.022), s), (0.009, 0.009, 0.009), "ghost_glow", segments=6, rings=4)
    spine = [(0.13, 0, 0.0), (0.0, 0, 0.005), (-0.13, 0, 0.0), (-0.26, 0, -0.005), (-0.31, 0, -0.005)]
    tube(m, "spine", spine, [0.012, 0.011, 0.009, 0.007, 0.006], "bone_shadow", segments=6, subsurf=0)
    for i, x in enumerate([0.11, 0.06, 0.01, -0.04, -0.09, -0.14, -0.19, -0.24]):
        sphere(m, "vertebra", (x, 0, 0.002), (0.012, 0.016, 0.016), "bone_white", segments=6, rings=4)
        h = 0.075 * (1 - i / 9)                                   # ribs shrink towards the tail
        if i < 6:
            for s in (1, -1):
                tube(m, "rib", [(x, 0, 0.0), (x - 0.008, s * 0.04, -0.012), (x - 0.016, s * 0.05, -h * 0.6), (x - 0.02, s * 0.03, -h)],
                     [0.006, 0.005, 0.0045, 0.003], "bone_white", segments=5, subsurf=0)
        tube(m, "spike", [(x, 0, 0.01), (x - 0.01, 0, 0.01 + h * 0.55)], [0.004, 0.0], "bone_white", segments=4, subsurf=0)
    # the ghostly membrane along its back and a wispy tail
    xs = [0.11, 0.06, 0.01, -0.04, -0.09, -0.14, -0.19, -0.24]
    ribbon(m, "wisp", [(x, 0, 0.012) for x in xs], [(x - 0.01, 0, 0.012 + 0.075 * (1 - i / 9) * 0.5) for i, x in enumerate(xs)], "ghost_wisp", 0.004)
    for a in (-0.7, -0.35, 0.0, 0.35, 0.7):
        tube(m, "tailbone", [(-0.3, 0, -0.005), (-0.39, 0, -0.005 + a * 0.13)], [0.005, 0.002], "bone_white", segments=4, subsurf=0)
    fin(m, [(0, 0.015), (-0.09, 0.1), (-0.11, 0.06), (-0.1, 0), (-0.11, -0.06), (-0.09, -0.1), (0, -0.015)], "ghost_wisp", 0.004, pos=(-0.3, 0, -0.005))


def shellmite(m):
    # a little sea bug: an armoured, segmented shell, short legs and long feelers (~0.24 m)
    m.flat = True
    rings = []
    for i in range(10):
        t = i / 9
        x = -0.11 + t * 0.21
        r = 0.035 + 0.04 * math.sin(math.pi * min(1.0, t * 1.05))
        bump = 1.08 if i % 2 else 1.0                              # the overlapping plates
        rings.append((x, r * 1.15 * bump, r * 0.75 * bump, 0.01))
    lathe(m, "shell", rings, 10, lambda r, a: "mite_belly" if math.sin(a) < -0.35 else ("mite_dark" if r % 2 else "mite_shell"),
          subsurf=0, offset=math.pi / 10)
    sphere(m, "head", (0.105, 0, 0.0), (0.03, 0.04, 0.026), "mite_dark", segments=8, rings=5)
    for s in (1, -1):
        eye(m, side((0.118, 0.028, 0.012), s), 0.01, side((0.6, 1, 0.3), s))
        tube(m, "feeler", [side((0.125, 0.012, 0.012), s), side((0.17, 0.04, 0.03), s), side((0.21, 0.07, 0.025), s), side((0.24, 0.09, 0.008), s)],
             [0.004, 0.003, 0.0025, 0.0015], "mite_leg", segments=4, subsurf=0)
        for x in (0.07, 0.04, 0.01, -0.02, -0.05, -0.08):
            tube(m, "leg", [side((x, 0.04, -0.01), s), side((x + 0.004, 0.07, -0.025), s), side((x + 0.01, 0.075, -0.05), s)],
                 [0.006, 0.005, 0.003], "mite_leg", segments=4, subsurf=0)
        fin(m, [(0, 0.008), (-0.04, 0.02), (-0.05, 0.0), (-0.02, -0.006)], "mite_dark", 0.004, rot=(s * 1.4, 0, s * 0.35), pos=(-0.11, s * 0.015, 0.008))


SPECIES = {
    "bonefin": bonefin,
    "shellmite": shellmite,
    "coralit": coralit,
    "inkling": inkling,
    "urchip": urchip,
    "driftray": driftray,
    "tidefang": tidefang,
    "frostfin": frostfin,
    "leviathorn": leviathorn,
}
