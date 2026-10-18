"""Stage the BackgroundProps textures into the Unity project.

Source (READ ONLY - never modified; another process is actively writing to it):
    ../collage game assets/collage game assets from web/BackgroundProps/

Naming convention, per asset folder:
    <Asset>/textures/<subasset>_<map>_<res>.<ext>
      <map> = diff      albedo
              nor_gl    normal map, authored OpenGL (+Y)
              arm       packed Ambient Occlusion / Roughness / Metallic
              emissive  emissive
      e.g. SchoolDesk_01_diff_1k.jpg
           standing_chalkboard_01_board_diff_1k.jpg

The manifest is keyed by the *sub-asset prefix*, not the parent asset, because
that is exactly how Unity's FBX importer names the materials it generates
(`standing_chalkboard_01_board`, `chemistry_set_glass`, `vintage_cabinet_01_a`,
...). Keying on the prefix makes the lookup a direct name match with no
guessing.

Normal maps are authored OpenGL (+Y up). Unity expects DirectX (+Y down), so the
manifest flags them and the Unity-side binder flips green on import.
"""

import json
import os
import re
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.join(HERE, "collage game")

SRC = os.path.normpath(os.path.join(
    HERE, "..", "collage game assets",
    "collage game assets from web", "BackgroundProps"))

DST = os.path.join(PROJECT, "Assets", "ThirdParty", "PolyhavenProps", "Textures")
MANIFEST = os.path.join(PROJECT, "Assets", "Editor", "PropTextureManifest.json")

# <prefix>_<map>[_<res>].<ext>   with map in
#   {diff, nor_gl, arm, emissive|emission, roughness|rough}
MAP_RE = re.compile(
    r"^(?P<prefix>.+?)_(?P<map>diff|nor_gl|arm|emission|emissive|roughness|rough)"
    r"(?:_(?P<res>\d+k))?\.(?:jpg|jpeg|png|tga)$"
)


def main():
    if not os.path.isdir(SRC):
        raise SystemExit("source not found: %s" % SRC)

    # Collect every asset folder that has a textures/ child.
    asset_dirs = []
    for root, dirs, _ in os.walk(SRC):
        if os.path.basename(root) == "textures":
            asset_dirs.append(os.path.dirname(root))
            dirs[:] = []                      # don't descend past textures/
    asset_dirs.sort()

    # prefix -> {map_kind: source_path}
    groups = {}
    stray = []

    for asset_dir in asset_dirs:
        tex_dir = os.path.join(asset_dir, "textures")
        for fn in sorted(os.listdir(tex_dir)):
            if fn.lower().endswith((".md", ".txt")):
                continue
            m = MAP_RE.match(fn)
            if not m:
                stray.append(os.path.join(os.path.basename(asset_dir), fn))
                continue
            kind = m.group("map")
            # these spelling variants all mean the same map type
            kind = "rough" if kind == "roughness" else kind
            kind = "emissive" if kind == "emission" else kind
            prefix = m.group("prefix")
            groups.setdefault(prefix, {})
            # first file wins, so a 2k never displaces an already-seen 1k
            groups[prefix].setdefault(kind, os.path.join(tex_dir, fn))

    # Copy images and record project-relative destinations.
    # The manifest is a FLAT list of {prefix, kind, path} so Unity's JsonUtility
    # can deserialise it with a plain DTO - no nested-dictionary parsing needed.
    os.makedirs(DST, exist_ok=True)
    items = []
    copied = 0
    groups_out = set()

    for prefix in sorted(groups):
        dst_dir = os.path.join(DST, prefix)
        for kind, src_path in sorted(groups[prefix].items()):
            fn = os.path.basename(src_path)
            dst_path = os.path.join(dst_dir, fn)
            if (not os.path.exists(dst_path)
                    or os.path.getsize(dst_path) != os.path.getsize(src_path)):
                os.makedirs(dst_dir, exist_ok=True)
                shutil.copy2(src_path, dst_path)
                copied += 1
            rel = os.path.relpath(dst_path, PROJECT).replace("\\", "/")
            items.append({"prefix": prefix, "kind": kind, "path": rel})
            groups_out.add(prefix)

    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump({"items": items}, f, indent=1, sort_keys=True)

    kinds = {}
    for maps in groups.values():
        for k in maps:
            kinds[k] = kinds.get(k, 0) + 1

    print("sub-asset groups : %d" % len(groups_out))
    print("files copied     : %d" % copied)
    print("manifest entries : %d" % len(items))
    print("map coverage     : %s" % ", ".join(
        "%s=%d" % (k, kinds[k]) for k in sorted(kinds)))
    print("manifest         : %s" % MANIFEST)
    if stray:
        print("\nunrecognised files (%d):" % len(stray))
        for s in stray:
            print("   " + s)


if __name__ == "__main__":
    main()