# -*- coding: utf-8 -*-
# 프로젝트 루트에서 실행: python BuildScripts/Verify/glyph_check.py  — 언어별로 번역 문구에 필요한 글자가 그 언어 폰트(폴백 포함)에 다 있는지 봅니다.
import re, io, os, glob, json, collections
ROOT = "."
# 1) guid -> 경로
guid2path = {}
for m in glob.glob("Assets/**/*.meta", recursive=True):
    try:
        g = re.search(r"guid: ([0-9a-f]{32})", io.open(m, encoding="utf-8", errors="ignore").read(1000))
        if g: guid2path[g.group(1)] = m[:-5].replace("\\", "/")
    except Exception: pass
# 2) Language enum
lm = io.open("Assets/Scripts/Application Layer/LocalizationSystem/LocalizationManager.cs", encoding="utf-8-sig").read()
code_of = dict(re.findall(r"case Language\.(\w+): return string\.IsNullOrEmpty\(_entry\.(\w+)\)", lm))
enum_src = None
for f in glob.glob("Assets/Scripts/**/*.cs", recursive=True):
    t = io.open(f, encoding="utf-8-sig", errors="ignore").read()
    m = re.search(r"enum\s+Language\b[^{]*\{(.*?)\}", t, re.S)
    if m: enum_src = m.group(1); break
names = []; v = -1; num2name = {}
for p in re.sub(r"//.*", "", enum_src).split(","):
    p = p.strip()
    if not p: continue
    if "=" in p: n, e = [x.strip() for x in p.split("=")]; v = int(e)
    else: n = p; v += 1
    num2name[v] = n
# 3) 폰트 테이블
tbl = io.open("Assets/Scriptable Obj/Localization/LocalizationFontTable.asset", encoding="utf-8").read()
lang_font = {int(a): b for a, b in re.findall(r"- language: (\d+)\s*\n\s*fontAsset: \{fileID: \d+(?:, guid: ([0-9a-f]+))?", tbl)}
# 4) 폰트 아틀라스 글자 + 폴백
cache = {}
def font_chars(guid, seen=None):
    seen = seen or set()
    if not guid or guid in seen or guid not in guid2path: return set()
    seen.add(guid)
    t = io.open(guid2path[guid], encoding="utf-8", errors="ignore").read()
    chars = set(int(x) for x in re.findall(r"m_Unicode: (\d+)", t[t.find("m_CharacterTable:"):]))
    fb = t[t.find("m_FallbackFontAssetTable:"):]
    fb = fb[:fb.find("\n  m_", 30)] if "\n  m_" in fb[30:] else fb[:2000]
    for g in re.findall(r"guid: ([0-9a-f]{32})", fb): chars |= font_chars(g, seen)
    return chars
# 기본 폰트 = TMP Settings 의 기본 폰트
tmps = glob.glob("Assets/**/TMP Settings.asset", recursive=True)
default_guid = None
if tmps:
    t = io.open(tmps[0], encoding="utf-8").read()
    m = re.search(r"m_defaultFontAsset: \{fileID: \d+, guid: ([0-9a-f]+)", t)
    default_guid = m.group(1) if m else None
# 5) 언어별 문자 수집
langchars = collections.defaultdict(set)
for p in glob.glob("Assets/Resources/Localization/*.json"):
    d = json.loads(open(p, "rb").read().decode("utf-8-sig"))
    def walk(x):
        if isinstance(x, dict):
            if "kr" in x and "en" in x:
                for k, val in x.items():
                    if isinstance(val, str): langchars[k] |= set(re.sub(r"<[^>]*>|\n|\{\d+\}", "", val))
            for y in x.values(): walk(y)
        elif isinstance(x, list):
            for y in x: walk(y)
    walk(d)
print("기본 폰트(TMP Settings):", os.path.basename(guid2path.get(default_guid, "?")))
for num in sorted(num2name):
    name = num2name[num]; code = code_of.get(name, name.lower())
    g = lang_font.get(num) or default_guid
    fname = os.path.basename(guid2path.get(g, "?")).replace(".asset", "")
    have = font_chars(g)
    need = set(c for c in langchars.get(code, set()) if not c.isspace())
    miss = sorted(c for c in need if ord(c) not in have)
    print("  %2d %-8s %-8s 폰트 %-26s 필요 %4d자  빠짐 %3d  %s" % (num, name, code, fname + ("(기본)" if not lang_font.get(num) else ""), len(need), len(miss), "".join(miss)[:40]))
