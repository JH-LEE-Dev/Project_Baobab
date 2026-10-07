# -*- coding: utf-8 -*-
"""
배포 빌드 안쪽(data.unity3d 등) 리소스 유출 검사 (Docs/BuildLeakReview.md 의 리소스 부분)

도용하는 사람과 같은 방법(UnityPy 로 에셋 추출)으로 빌드를 열어, 그림·소리·데이터 이름을 뽑고
데모에 있으면 안 되는 것을 찾습니다. 그림 몇 개와 소리 하나를 실제로 꺼내 "추출이 된다"는 것도 확인합니다.

준비 (한 번만, 저장소 밖 아무 폴더에):
  python -m venv <폴더>/upy
  <폴더>/upy/Scripts/python -m pip install UnityPy

사용법:
  <upy 파이썬> BuildScripts/Verify/extract_check.py "C:/Unity Build/STEAM_DEMO" <출력 폴더> [이전 출력 폴더]

  - 출력 폴더에 names.json(종류별 이름), texts.json(TextAsset 내용), 꺼낸 그림·소리를 저장합니다.
  - 이전 출력 폴더를 주면 그림 이름이 무엇이 빠지고 늘었는지 비교합니다. 직전 배포 빌드와 비교하십시오.

종료 코드: 데모 빌드에서 미공개 콘텐츠가 나오면 1.
"""
import collections
import json
import os
import re
import sys

import UnityPy

# ---------------------------------------------------------------------------
# 데모에 있으면 안 되는 그림 이름입니다. 데모는 1스테이지(OakTree·PineTree·BirchTree)까지만 공개합니다.
# 미공개 나무·지역·기능이 늘면 여기에 추가하십시오. (기준: DemoContentStripper 의 DemoFeatureScope)
#   - Wood04~12          : 미공개 나무 원목 아이콘 (1.0.3 데모까지 실려 있었음, 2026-10-07 제거)
#   - Timber_<미공개 나무> : 미공개 나무 통나무 그림
#   - Wood_*_Stage04     : 4스테이지 보석 원목
# ---------------------------------------------------------------------------
DEMO_LOCKED_IMAGE_PREFIXES = (
    ["Wood%02d" % i for i in range(4, 13)]
    + ["Timber_" + t for t in ("SporepuffTree", "FluffyMyceliumTree", "BellpineTree", "StarrootTree", "MoonhaloTree",
                               "GalaxygrainTree", "CinderTree", "LavasapTree", "ObsidianTree")]
    + ["Wood_Gold_Stage04", "Wood_Diamond_Stage04", "Wood_Rainbow_Stage04"]
)

# 데모 빌드에 있으면 안 되는 데이터 이름 (정식판 DB)
DEMO_FORBIDDEN_DATA = re.compile(r"_Full$|SkillDataBase_Full|AbilityNodeDatabase_Full")


def read_stamp(_root):
    _stamp = {}
    _path = os.path.join(_root, "BUILD_STAMP.txt")
    if os.path.isfile(_path):
        for _line in open(_path, encoding="utf-8", errors="replace"):
            if "=" in _line and not _line.startswith("#"):
                _k, _v = _line.rstrip("\n").split("=", 1)
                _stamp[_k.strip()] = _v.strip()
    return _stamp


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2

    _root, _out = sys.argv[1], sys.argv[2]
    _prev = sys.argv[3] if len(sys.argv) > 3 else None
    os.makedirs(_out, exist_ok=True)
    _stamp = read_stamp(_root)
    _is_demo = "Demo" == _stamp.get("RELEASE")

    _env = UnityPy.load(os.path.join(_root, "LumberBoy_Data"))
    _count = collections.Counter()
    _names = collections.defaultdict(set)
    _texts = {}
    _exported = {"Sprite": 0, "AudioClip": 0}

    for _obj in _env.objects:
        _t = _obj.type.name
        _count[_t] += 1
        if _t not in ("Sprite", "Texture2D", "AudioClip", "TextAsset", "MonoBehaviour", "Material", "Shader", "AnimationClip"):
            continue
        try:
            _d = _obj.read()
        except Exception:
            continue
        _n = getattr(_d, "m_Name", None) or getattr(_d, "name", None) or ""
        _names[_t].add(_n)

        if "TextAsset" == _t:
            _s = _d.m_Script
            if isinstance(_s, bytes):
                _s = _s.decode("utf-8", "replace")
            _texts[_n] = _s
        elif "Sprite" == _t and _exported["Sprite"] < 3:
            try:
                _d.image.save(os.path.join(_out, "sprite_%d_%s.png" % (_exported["Sprite"], re.sub(r"[^\w.-]", "_", _n))))
                _exported["Sprite"] += 1
            except Exception:
                pass
        elif "AudioClip" == _t and _exported["AudioClip"] < 1:
            try:
                for _fn, _data in _d.samples.items():
                    open(os.path.join(_out, "audio_" + re.sub(r"[^\w.-]", "_", _fn)), "wb").write(_data)
                    _exported["AudioClip"] += 1
                    break
            except Exception:
                pass

    json.dump({_t: sorted(_v) for _t, _v in _names.items()}, open(os.path.join(_out, "names.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    json.dump(_texts, open(os.path.join(_out, "texts.json"), "w", encoding="utf-8"), ensure_ascii=False)

    print("빌드: %s  (STORE=%s RELEASE=%s GIT=%s)" % (_root, _stamp.get("STORE"), _stamp.get("RELEASE"), _stamp.get("GIT")))
    print("== 종류별 개수: " + ", ".join("%s %d" % (_t, _c) for _t, _c in _count.most_common(12)))
    print("== 실제로 꺼낸 파일: 그림 %d, 소리 %d  (꺼낼 수 있다 = 리소스는 보호되지 않는다는 뜻. 정상)" % (_exported["Sprite"], _exported["AudioClip"]))
    print("== TextAsset: " + ", ".join(sorted(_texts)))

    _fails = []
    _images = _names["Sprite"] | _names["Texture2D"]

    if _is_demo:
        _locked = sorted(_x for _x in _images if any(_x == _p or _x.startswith(_p + "_") for _p in DEMO_LOCKED_IMAGE_PREFIXES))
        if _locked:
            _fails.append("데모에 미공개 그림: " + ", ".join(_locked))
        _full = sorted(_x for _x in (_names["MonoBehaviour"] | set(_texts)) if DEMO_FORBIDDEN_DATA.search(_x))
        if _full:
            _fails.append("데모에 정식판 데이터: " + ", ".join(_full))

    if _prev and os.path.isfile(os.path.join(_prev, "names.json")):
        _old = json.load(open(os.path.join(_prev, "names.json"), encoding="utf-8"))
        _old_images = set(_old.get("Sprite", [])) | set(_old.get("Texture2D", []))
        print("== 이전 빌드 대비 빠진 그림 (%d): %s" % (len(_old_images - _images), ", ".join(sorted(_old_images - _images)[:60])))
        print("== 이전 빌드 대비 늘어난 그림 (%d): %s" % (len(_images - _old_images), ", ".join(sorted(_images - _old_images)[:60])))
        print("   늘어난 그림이 공개 범위 안의 것인지 사람이 확인하십시오.")

    if _fails:
        for _f in _fails:
            print("FAIL " + _f)
        return 1

    print("OK   %s" % ("미공개 그림·정식판 데이터 없음" if _is_demo else "정식 빌드 - 미공개 콘텐츠 검사는 데모에만 적용"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
