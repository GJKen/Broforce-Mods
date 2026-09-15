"""Temporary helper: find HeroType enum (playable bro list) in Broforce game DLLs."""
import re, os

GAME_MANAGED = r"E:\SteamLibrary\steamapps\common\Broforce\Broforce_beta_Data\Managed"

# Patterns: any CamelCase word that contains common bro-ish substrings is a candidate
BRO_HINTS = re.compile(
    r"[A-Z][A-Za-z0-9_]*(?:rambro|roboCop|brommando|broracus|brodread|brolan|broskin|"
    r"broce|broGummer|broLee|bro[AX]|brofessional|brolander|brolliams|ripbro|brenan|"
    r"brodock|broden|broffy|broguy|broMax|BroMax|brochete|brocketeer|brominator|"
    r"Tollbro|Timebro|DirtyHarry|Predabro|Seagal|Browler|Hale|Brones|Indiana|Snake|"
    r"Brobo|BroHome|Brother|BroDown|BroUp|BroZip|Brocer|Broce)[A-Za-z0-9_]*",
    re.I,
)

# Ordered rough known candidate bro stems to detect
KNOWN = [
    "Rambro", "Brommando", "Broracus", "Brodred", "Brolander", "BroMax",
    "Broce", "Desperabro", "BroLee", "DirtyHarry", "BroSkin", "TimeBro",
    "BroBoCop", "Brofessional", "Brolliams", "Ripbro", "Indianna", "Bronan",
    "Predabro", "Broden", "Broffy", "BroGummer", "HaleTheBro", "Broc",
    "Conrad", "Tollbro", "AshBrolliams", "Ellen", "Brochete", "Brocketeer",
]

hint_re = re.compile("|".join(re.escape(k) for k in KNOWN), re.I)

for f in sorted(os.listdir(GAME_MANAGED)):
    if not f.lower().endswith(".dll"):
        continue
    if not ("CSharp" in f or "Assembly" in f):
        continue
    path = os.path.join(GAME_MANAGED, f)
    with open(path, "rb") as fh:
        data = fh.read()
    for enc in ("utf-16-le", "latin-1"):
        try:
            s = data.decode(enc, errors="ignore")
        except Exception:
            continue
        found = set()
        for m in hint_re.finditer(s):
            t = m.group(0)
            if len(t) >= 3 and len(t) <= 40:
                found.add(t)
        if found:
            print(f"== {f} ({enc}) ==")
            for x in sorted(found):
                print("  ", x)