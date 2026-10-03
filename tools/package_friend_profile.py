import json
import os
import pathlib
import re
import shutil
import sys

settings_path = pathlib.Path(sys.argv[1])
package_dir = pathlib.Path(sys.argv[2])
source_assets = settings_path.parent / "assets"
bundle_dir = package_dir / "data"
bundle_assets = bundle_dir / "assets"
if (bundle_dir / "settings.json").exists() or (bundle_assets.exists() and any(bundle_assets.iterdir())):
    raise RuntimeError("Target data folder already contains settings or images; choose a clean package")
bundle_assets.mkdir(parents=True, exist_ok=True)

data = json.loads(settings_path.read_text(encoding="utf-8-sig"))
image_paths = set()
groups = data.get("ImageGroups", []) + (data.get("AppliedSettings") or {}).get("ImageGroups", [])
for group in groups:
    for images in group.get("Images", {}).values():
        image_paths.update(path for path in images if isinstance(path, str))
    for images in group.get("Backgrounds", {}).values():
        image_paths.update(path for path in images if isinstance(path, str))

# These old fields are retained for migration but do not drive the current app.
data["Images"] = {}
data["CarouselModes"] = {}
for profile in data.get("Profiles", []):
    profile["Images"] = {}
    profile["CarouselModes"] = {}
    profile["ImageIndices"] = {}
    profile["HasTriggered"] = {}
replacements = {}
names = {}
for index, original in enumerate(sorted(image_paths, key=str.casefold), 1):
    source = pathlib.Path(original)
    if not source.is_absolute():
        source = settings_path.parent / source
    if not source.is_file() or source_assets.resolve() not in source.resolve().parents:
        raise RuntimeError("Referenced image is missing or outside the app's assets directory")
    filename = f"image-{index:02d}{source.suffix.lower()}"
    shutil.copyfile(source, bundle_assets / filename)
    relative = "assets/" + filename
    replacements[original] = relative
    names[relative] = f"素材 {index:02d}{source.suffix.lower()}"

def rewrite(node):
    if isinstance(node, dict):
        return {replacements.get(key, key): rewrite(value) for key, value in node.items()}
    if isinstance(node, list):
        return [rewrite(value) for value in node]
    if isinstance(node, str):
        return replacements.get(node, node)
    return node

data = rewrite(data)
data["ImageDisplayNames"] = names
data["InputCount"] = 0
data["PetX"] = None
data["PetY"] = None

def reset_playback(node):
    if isinstance(node, dict):
        if isinstance(node.get("ImageIndices"), dict):
            node["ImageIndices"] = {key: 0 for key in node["ImageIndices"]}
        if isinstance(node.get("HasTriggered"), dict):
            node["HasTriggered"] = {key: False for key in node["HasTriggered"]}
        if isinstance(node.get("BackgroundIndices"), dict):
            node["BackgroundIndices"] = {key: 0 for key in node["BackgroundIndices"]}
        if isinstance(node.get("BackgroundHasTriggered"), dict):
            node["BackgroundHasTriggered"] = {key: False for key in node["BackgroundHasTriggered"]}
        for value in node.values():
            reset_playback(value)
    elif isinstance(node, list):
        for value in node:
            reset_playback(value)

reset_playback(data)
serialized = json.dumps(data, ensure_ascii=False, indent=2)
if re.search(r"[A-Za-z]:\\", serialized) or str(settings_path.parent) in serialized:
    raise RuntimeError("A local absolute path remains in the shareable settings")
(bundle_dir / "settings.json").write_text(serialized, encoding="utf-8")
(bundle_dir / "counter.txt").write_text("0", encoding="utf-8")
print(f"images={len(image_paths)} settingsBytes={len(serialized.encode('utf-8'))}")
