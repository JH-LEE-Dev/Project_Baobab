// ─────────────────────────────────────────────────────────────────────────────
// VFX 골든 캡처(결과 불변 검증) 도구. 에디터 전용이라 빌드에는 컴파일되지 않습니다.
// 쓰는 법과 원리는 VfxGoldenRunner.cs 머리 주석을 보세요.
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Diagnostics.VfxGolden
{
    /// <summary>
    /// 골든 캡처 케이스 하나입니다. 이펙트 하나(또는 한 상황)를 고정 시드·고정 dt로 정해진 프레임 수만큼 재생합니다.
    ///
    /// [지켜야 할 것]
    /// - 만든 오브젝트는 전부 Context.Root 아래에 둡니다. 케이스가 끝나면 Root를 통째로 지웁니다.
    /// - 난수는 UnityEngine.Random만 쓰고(러너가 케이스 시작 때 InitState로 고정), 시간 경과는 프레임 번호로 셉니다.
    /// - 이펙트 코드는 고치지 않습니다. 케이스는 게임이 부르는 공개 API를 그대로 불러야 검증이 의미 있습니다.
    /// </summary>
    public abstract class VfxGoldenCase
    {
        /// <summary>결과 파일 이름이자 비교 키입니다. 케이스끼리 겹치면 안 됩니다.</summary>
        public abstract string Name { get; }

        /// <summary>재생할 프레임 수(고정 dt 1/60초 기준)입니다.</summary>
        public virtual int Frames => 90;

        /// <summary>캡처 카메라가 보는 월드 중심입니다.</summary>
        public virtual Vector2 ViewCenter => Vector2.zero;

        /// <summary>캡처 카메라가 보는 월드 크기(유닛)입니다. 월드 텍셀 1개(1/32유닛)가 RT 픽셀 2x2가 되도록 RT 크기를 정합니다.</summary>
        public virtual Vector2 ViewSize => new Vector2(12.0f, 12.0f);

        /// <summary>케이스 시작 프레임에 한 번 불립니다(Update 뒤, LateUpdate 앞). 이펙트를 만들고 재생을 겁니다.</summary>
        public abstract void Setup(VfxGoldenContext _context);

        /// <summary>시작 프레임 다음부터 매 프레임 불립니다(_frame은 1부터). 재트리거·카메라 이동 같은 시나리오에 씁니다.</summary>
        public virtual void OnFrame(VfxGoldenContext _context, int _frame) { }
    }

    /// <summary>
    /// 케이스가 쓰는 도구 모음입니다. 프리팹 생성과 파티클 시드 고정을 대신해 줍니다.
    /// </summary>
    public class VfxGoldenContext
    {
        private const string InstallerPrefabPath = "Assets/Prefabs/Installer/Installer/GameInstaller.prefab";

        // //내부 의존성
        private readonly List<ParticleSystem> particleBuffer = new List<ParticleSystem>(32);
        private readonly Dictionary<string, VFXComponent> pools = new Dictionary<string, VFXComponent>(8);

        public Transform Root { get; private set; }
        /// <summary>Root 밖에 있지만 이 케이스가 만든 오브젝트(풀 재생으로 월드에 떼어진 인스턴스 등)입니다.</summary>
        public List<GameObject> ExtraRoots { get; } = new List<GameObject>(8);
        public Camera CaptureCamera { get; private set; }
        public uint CaseSeed { get; private set; }

        public void Setup(Transform _root, Camera _camera, uint _caseSeed)
        {
            Root = _root;
            CaptureCamera = _camera;
            CaseSeed = _caseSeed;
            pools.Clear();
            ExtraRoots.Clear();
        }

        /// <summary>
        /// 프리팹을 Root 아래에 만듭니다. 비활성 부모 아래에서 만든 뒤 모든 ParticleSystem의 시드를 고정하고 켭니다.
        /// (playOnAwake 파티클이 자동 시드로 먼저 시작하지 않게 하기 위함입니다.)
        /// </summary>
        public GameObject Spawn(string _assetPath, Vector3 _position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath);
            if (null == prefab)
            {
                throw new System.IO.FileNotFoundException("[VfxGolden] 프리팹이 없습니다: " + _assetPath);
            }

            GameObject holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            holder.transform.SetParent(Root, false);

            GameObject instance = Object.Instantiate(prefab, _position, Quaternion.identity, holder.transform);
            FixParticleSeeds(instance);

            instance.transform.SetParent(Root, true);
            Object.Destroy(holder);
            return instance;
        }

        /// <summary>빈 오브젝트를 Root 아래에 만듭니다. 런타임에 AddComponent로 만드는 이펙트용입니다.</summary>
        public GameObject CreateEmpty(string _name, Vector3 _position)
        {
            GameObject go = new GameObject(_name);
            go.transform.SetParent(Root, false);
            go.transform.position = _position;
            return go;
        }

        /// <summary>
        /// 게임과 같은 풀 경로(VFXComponent.Play)로 재생합니다. GameInstaller 프리팹에 등록된 태그 설정을 그대로 쓰되,
        /// 예열 수만 0으로 바꿔 Root 아래에 전용 VFXComponent를 만듭니다(예열 수를 바꾸는 최적화가 결과 비교에 끼어들지 않게).
        /// 인스턴스를 꺼낸 뒤 파티클 시드를 고정하고, Random을 다시 고정한 다음 재생합니다.
        /// </summary>
        public ParticleSystem PlayPooled(VFXPlaySettings _settings)
        {
            VFXComponent pool = GetOrCreatePool(_settings.VfxTag);
            ParticleSystem effect = pool.Get(_settings.VfxTag);
            if (null == effect)
            {
                throw new System.InvalidOperationException("[VfxGolden] 풀에서 꺼내지 못했습니다: " + _settings.VfxTag);
            }

            FixParticleSeeds(effect.gameObject);
            Random.InitState((int)CaseSeed);
            pool.Play(effect, _settings);

            // 게임처럼 부모 없이(월드로) 떼어 재생하므로 Root 밖에 놓인다. 해시와 정리 대상에 따로 올린다.
            Track(effect.transform.root.gameObject);
            return effect;
        }

        /// <summary>Root 밖에 놓인 오브젝트를 해시·정리 대상에 올립니다(이미 Root 아래이거나 올라가 있으면 무시).</summary>
        public void Track(GameObject _go)
        {
            // Root의 조상(러너 등)을 올리면 케이스 정리 때 러너까지 지워지므로 막는다.
            if (null == _go || _go.transform.IsChildOf(Root) || Root.IsChildOf(_go.transform) || ExtraRoots.Contains(_go)) return;
            ExtraRoots.Add(_go);
        }

        private VFXComponent GetOrCreatePool(string _tag)
        {
            if (pools.TryGetValue(_tag, out VFXComponent existing) && null != existing) return existing;

            VFXPoolData source = FindInstallerPoolData(_tag);
            if (null == source)
            {
                throw new System.ArgumentException("[VfxGolden] GameInstaller에 없는 VFX 태그입니다: " + _tag);
            }

            VFXPoolData copy = new VFXPoolData();
            foreach (FieldInfo field in typeof(VFXPoolData).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                field.SetValue(copy, field.GetValue(source));
            }
            typeof(VFXPoolData).GetField("initialPoolSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, 0);

            GameObject go = new GameObject("[Pool] " + _tag);
            go.SetActive(false);
            go.transform.SetParent(Root, false);
            VFXComponent pool = go.AddComponent<VFXComponent>();
            typeof(VFXComponent).GetField("initializeOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pool, false);
            typeof(VFXComponent).GetField("vfxPoolDataList", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pool, new List<VFXPoolData> { copy });
            go.SetActive(true);
            pool.Initialize();

            pools[_tag] = pool;
            return pool;
        }

        private static VFXPoolData FindInstallerPoolData(string _tag)
        {
            GameObject installer = AssetDatabase.LoadAssetAtPath<GameObject>(InstallerPrefabPath);
            if (null == installer) return null;

            FieldInfo listField = typeof(VFXComponent).GetField("vfxPoolDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            VFXComponent[] components = installer.GetComponentsInChildren<VFXComponent>(true);
            for (int i = 0; i < components.Length; i++)
            {
                List<VFXPoolData> list = listField.GetValue(components[i]) as List<VFXPoolData>;
                if (null == list) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    if (null != list[k] && _tag == list[k].VfxTag) return list[k];
                }
            }
            return null;
        }

        /// <summary>_root 아래 모든 ParticleSystem의 자동 시드를 끄고 케이스 시드에서 정한 고정 시드를 넣습니다.</summary>
        public void FixParticleSeeds(GameObject _root)
        {
            particleBuffer.Clear();
            _root.GetComponentsInChildren(true, particleBuffer);
            for (int i = 0; i < particleBuffer.Count; i++)
            {
                ParticleSystem ps = particleBuffer[i];
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = unchecked(CaseSeed * 2654435761u + (uint)i * 40503u + 1u);
            }
        }
    }
}

#endif
