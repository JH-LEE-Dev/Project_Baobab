using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>본편 연출을 그대로 쓰지 못하는(코드에서 꺼낼 수 없는) 에셋만 따로 꽂는다.</summary>
[Serializable]
public sealed class RoguelikeSkillVfxAssets
{
    [Tooltip("본편 회전베기 프레임(AttackComponent.whirlwindFrames와 같은 것). 회전베기+부메랑 타격에 쓴다.")]
    public Sprite[] whirlwindFrames;
    [Tooltip("포자 폭탄이 날아가는 동안의 그림(본편 포자 그림).")]
    public Sprite sporeBombSprite;
    [Tooltip("포자 폭탄이 날아가는 시간(초)과 포물선 높이.")]
    public float sporeBombThrowSeconds = 0.45f;
    public float sporeBombArcHeight = 1.6f;
    [Tooltip("연막이 유지되는 동안 포자가 피어오르는 간격(초).")]
    public float sporeZonePuffInterval = 0.3f;
    [Tooltip("본편 드론 레이저(VFX_DroneLaser.prefab). 스텔라+드론의 이음선 빔 - 기획서 \"드론 레이저가 이음선 위로 발사되는 형태\".")]
    public PresentationLayer.VFX.VFX_DroneLaser droneLaserPrefab;
}

/// <summary>
/// 로그라이크 신규 스킬·조합의 연출을 본편 연출로 재생한다(기획서 3-3 "본편 ~을 그대로 사용한다/활용한다").
///   스텔라 별: 본편 3스테이지 별 표식 나무에서 나오는 땅 별(TreeStarMark_Ground) + 등장음 / 사라질 때 본편 회전 소멸.
///   별 공격: 본편 별똥별 착지 폭발(StarImpactExplosion) + 별 폭발음.
///   포자 폭발(스텔라+포자폭탄): 본편 포자 폭발(4연발 + 포자 폭발음).
///   포자 폭탄: 포물선으로 던져 착지 때 포자 폭발, 연막 동안 포자가 피어오른다.
///   별 이음선 빔·노바(스텔라+드론): 본편 드론 레이저(+ 드론 발사음)·별자리 노바 마법진, 이음선은 본편 별자리 점선.
///   회전베기+부메랑: 본편 회전베기 VFX + 카메라 흔들림.
/// 판정·피해는 RoguelikeSkillController가 이미 끝낸 뒤라 여기서는 그리기만 한다. 이펙트 담당은 같은 지점의
/// RoguelikeSkillVfxSignal로 덧붙이거나 바꾼다.
/// </summary>
public sealed class RoguelikeSkillVfxPlayer
{
    // 본편 별자리 그룹 번호(0부터 증가)와 겹치지 않는 구간.
    private const int StarGroupBase = -100000;
    private const int LinkGroupBase = -200000;
    private const int SporeBursts = 4;
    private const float SporeBurstInterval = 0.15f;
    private const float SporeBurstSpread = 0.25f;

    private InDungeonVFXManager vfx;
    private ConstellationPixelLaserCreator laserCreator;
    private RoguelikeSkillVfxAssets assets;
    private MonoBehaviour host;
    private readonly List<Vector3> linkPoints = new List<Vector3>(2) { Vector3.zero, Vector3.zero };
    private int registeredLinkCount;
    private readonly List<GameObject> projectilePool = new List<GameObject>(4);

    // 드론 레이저 한 번이 다 그려지는 시간(본편 VFX_DroneLaser 0.05 + 0.10 + 0.15초)보다 조금 길게. 그 안이면 쓰는 중으로 본다.
    private const float DroneLaserBusySeconds = 0.35f;
    private readonly List<PresentationLayer.VFX.VFX_DroneLaser> droneLasers = new List<PresentationLayer.VFX.VFX_DroneLaser>(8);
    private readonly List<float> droneLaserLastPlay = new List<float>(8);
    private int lastBeamSoundFrame = -1;
    // 회전베기+부메랑: 같은 프레임에 여러 그루를 맞혀 회전베기가 여러 번 나가도 그림·흔들림은 한 번만(겹치면 같은 그림이다).
    private int lastWhirlwindFrame = -1;
    // 포자 연출 회차. StopCombatEffects가 올리면 진행 중이던 포자 폭발·폭탄·연막 연출이 다음 단계에서 멈춘다.
    private int sporeGeneration;

    /// <summary>본편 땅 별을 쓸 수 있어 로그라이크 별 자체 그림을 숨겨도 되는지.</summary>
    public bool bUsesMainStarVisual => null != vfx;

    // 땅 별 그림 중심이 별 위치(판정·스폰 지점)에서 떠 있는 거리. 이음선·빔·별 폭발은 그림 중심끼리 잇는다.
    private Vector3 starCenterOffset;

    public void Initialize(InDungeonObjectManager _objectManager, RoguelikeSkillVfxAssets _assets, MonoBehaviour _host)
    {
        assets = _assets ?? new RoguelikeSkillVfxAssets();
        host = _host;
        vfx = null != _objectManager ? _objectManager.GetComponentInChildren<InDungeonVFXManager>(true) : null;
        laserCreator = null != _objectManager ? _objectManager.GetComponentInChildren<ConstellationPixelLaserCreator>(true) : null;
        starCenterOffset = null != vfx ? vfx.ConstellationGroundMarkVisualOffset : Vector3.zero;
    }

    public void Play(in RoguelikeSkillVfxSignal _signal, IReadOnlyList<RoguelikeStar> _stars, IReadOnlyList<Vector2Int> _links)
    {
        switch (_signal.type)
        {
            case ERoguelikeSkillVfx.StarSpawned:
                vfx?.PlayConstellationGroundMarkVFX(_signal.position, Order(_signal.position) + 20, StarGroupBase - _signal.id);
                Sound.Play(SoundID.Starappear, _signal.position);
                break;

            case ERoguelikeSkillVfx.StarBlast:
                vfx?.BeginConstellationGroundMarkManifest(StarGroupBase - _signal.id);
                vfx?.PlayStarImpactExplosionVFX(_signal.position + starCenterOffset, Order(_signal.position) + 40);
                Sound.Play(SoundID.StarExplosion, _signal.position + starCenterOffset);
                CameraMoveController.Instance?.ShakeCamera(3f, 0.2f);
                break;

            case ERoguelikeSkillVfx.StarExpired:
                vfx?.BeginConstellationGroundMarkManifest(StarGroupBase - _signal.id);
                break;

            case ERoguelikeSkillVfx.LoneStarPulse:
                vfx?.PlayStarImpactExplosionVFX(_signal.position + starCenterOffset, Order(_signal.position) + 40);
                Sound.Play(SoundID.StarExplosion, _signal.position + starCenterOffset, 0.6f);
                break;

            case ERoguelikeSkillVfx.SporeExplosion:
                if (null != host && host.isActiveAndEnabled) host.StartCoroutine(SporeBurstRoutine(_signal.position, sporeGeneration));
                break;

            case ERoguelikeSkillVfx.SporeZoneStarted:
                if (null != host && host.isActiveAndEnabled) host.StartCoroutine(SporeBombRoutine(_signal.position2, _signal.position, _signal.radius, _signal.duration, sporeGeneration));
                break;

            case ERoguelikeSkillVfx.StarLinkBeam:
                FireLaser(_signal.position + starCenterOffset, _signal.position2 + starCenterOffset);
                break;

            case ERoguelikeSkillVfx.StarNova:
                vfx?.PlayConstellationNovaVFX(_signal.position, _signal.radius);
                Sound.Play(SoundID.StarExplosion, _signal.position);
                CameraMoveController.Instance?.ShakeCamera(6f, 0.35f);
                break;

            case ERoguelikeSkillVfx.StarLinksChanged:
                RebuildLinkLines(_stars, _links);
                break;

            case ERoguelikeSkillVfx.SpinBoomerangStrike:
                if (lastWhirlwindFrame == Time.frameCount) break;
                lastWhirlwindFrame = Time.frameCount;
                if (null != assets.whirlwindFrames && assets.whirlwindFrames.Length > 0) WhirlwindVFX.Spawn(_signal.position, _signal.radius, assets.whirlwindFrames);
                CameraMoveController.Instance?.ShakeCamera(2f, 0.15f);
                break;
        }
    }

    /// <summary>
    /// 남아 있는 별(스텔라+드론 - 노바 때까지 웨이브를 넘어 유지된다)의 땅 별과 이음선 점선을 다시 그린다. 본편 나무 정리
    /// (InDungeonObjectManager.ClearTrees)가 땅 별·점선을 일괄 회수하므로 그 뒤에 부른다.
    /// </summary>
    public void RestoreStars(IReadOnlyList<RoguelikeStar> _stars, IReadOnlyList<Vector2Int> _links)
    {
        if (null == vfx || null == _stars) return;

        for (int i = 0; i < _stars.Count; i++)
        {
            Vector3 pos = _stars[i].transform.position;
            // 등장 연출 없이 내려앉은 모습으로 다시 놓는다(새로 생기는 별처럼 튀어나오지 않게).
            vfx.PlayConstellationGroundMarkLandedForRun(pos, Order(pos) + 20, StarGroupBase - _stars[i].StarId);
        }

        // 본편 정리가 점선을 이미 거뒀다. 지운 적 없는 번호를 다시 지우지 않게 등록 수를 비우고 새로 깐다.
        registeredLinkCount = 0;
        RebuildLinkLines(_stars, _links);
    }

    /// <summary>
    /// 전투가 끝났다(웨이브·런 종료). 웨이브와 함께 끝나는 연출(포자 폭발·폭탄·연막)을 멈춘다. 별은 건드리지 않는다
    /// (스텔라+드론의 별은 웨이브를 넘어 남는다 - Clear는 따로 부른다).
    /// </summary>
    public void StopCombatEffects()
    {
        sporeGeneration++;
        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (null != projectilePool[i]) projectilePool[i].SetActive(false);
        }
    }

    /// <summary>웨이브가 끝나 별·이음선이 한꺼번에 치워질 때. 남은 땅 별과 점선을 거둔다.</summary>
    public void Clear()
    {
        if (null == vfx) return;

        vfx.ClearAllConstellationGroundMarks();
        RemoveLinkLines();
    }

    // // 내부

    private static int Order(Vector3 _position) => -Mathf.RoundToInt(_position.y * 100f);

    /// <summary>
    /// 이음선 하나에 본편 드론 레이저를 쏜다. 레이저 한 개는 다시 쏘면 처음부터 다시 터지므로(본편 Drone.chainZap) 이음선마다
    /// 따로 둔다. 발사음은 본편 드론 발사음(발사·타격·전기 세 겹)을 한 번의 발사(같은 프레임의 이음선 전부)에 한 번만 낸다.
    /// </summary>
    private void FireLaser(Vector3 _from, Vector3 _to)
    {
        PresentationLayer.VFX.VFX_DroneLaser laser = GetDroneLaser();
        if (null != laser)
        {
            laser.PlayZap(_from, _to);
        }
        else
        {
            // 드론 레이저 에셋이 없으면 본편 별자리 레이저로 대신 그린다.
            PresentationLayer.VFX.ConstellationPixelLaser fallback = null != laserCreator ? laserCreator.Get() : null;
            if (null != fallback) fallback.PlayLaser(_from, _to);
        }

        if (lastBeamSoundFrame == Time.frameCount) return;
        lastBeamSoundFrame = Time.frameCount;

        Vector3 mid = (_from + _to) * 0.5f;
        Sound.Play(SoundID.SFXBeamFire, mid);
        Sound.Play(SoundID.SFXPunchImpact, mid);
        Sound.Play(SoundID.SFXVoltageImpact, mid);
    }

    private PresentationLayer.VFX.VFX_DroneLaser GetDroneLaser()
    {
        if (null == assets.droneLaserPrefab || null == host) return null;

        float now = Time.time;
        for (int i = 0; i < droneLasers.Count; i++)
        {
            if (null == droneLasers[i] || now - droneLaserLastPlay[i] < DroneLaserBusySeconds) continue;
            droneLaserLastPlay[i] = now;
            return droneLasers[i];
        }

        PresentationLayer.VFX.VFX_DroneLaser created = UnityEngine.Object.Instantiate(assets.droneLaserPrefab, host.transform);
        created.transform.localPosition = Vector3.zero;
        droneLasers.Add(created);
        droneLaserLastPlay.Add(now);
        return created;
    }

    private void RebuildLinkLines(IReadOnlyList<RoguelikeStar> _stars, IReadOnlyList<Vector2Int> _links)
    {
        if (null == vfx) return;

        RemoveLinkLines();
        if (null == _stars || null == _links || 0 == _links.Count) return;

        // 별자리 점선 전체의 표시 여부("별자리 발현" 특성)는 건드리지 않는다 - 그러면 같은 필드의 본편 별자리 점선까지
        // 켜지거나 꺼진다. 로그라이크 이음선만 늘 보이게 등록한다.
        for (int i = 0; i < _links.Count; i++)
        {
            Vector2Int link = _links[i];
            if (link.x < 0 || link.y < 0 || link.x >= _stars.Count || link.y >= _stars.Count) continue;

            linkPoints[0] = _stars[link.x].transform.position + starCenterOffset;
            linkPoints[1] = _stars[link.y].transform.position + starCenterOffset;
            vfx.RegisterConstellationDottedLineAlwaysVisibleForRun(LinkGroupBase - i, linkPoints);
            registeredLinkCount = i + 1;
        }
    }

    private void RemoveLinkLines()
    {
        if (null == vfx) return;
        for (int i = 0; i < registeredLinkCount; i++) vfx.RemoveConstellationDottedLine(LinkGroupBase - i);
        registeredLinkCount = 0;
    }

    /// <summary>본편 포자 폭발(InDungeonObjectManager.ShieldExplosionRoutine)과 같은 4연발: 좌우를 번갈아 퍼지며 폭발음이 난다.</summary>
    private IEnumerator SporeBurstRoutine(Vector3 _position, int _generation)
    {
        if (null == vfx) yield break;

        for (int i = 0; i < SporeBursts; i++)
        {
            if (_generation != sporeGeneration) yield break;

            float side = 0 == (i & 1) ? -1f : 1f;
            Vector2 offset = new Vector2(side * SporeBurstSpread, (i < 2 ? 0.5f : 0f) + UnityEngine.Random.Range(-0.1f, 0.1f));
            Vector3 pos = _position + (Vector3)offset;
            vfx.PlaySporeExplosionVFX(pos, offset);
            Sound.Play(SoundID.SporeExplosion, pos);

            yield return new WaitForSeconds(SporeBurstInterval);
        }
    }

    /// <summary>캐릭터 쪽에서 포자 폭탄을 포물선으로 던진다. 떨어지면 포자가 터지고, 연막 동안 포자가 피어오른다.</summary>
    private IEnumerator SporeBombRoutine(Vector3 _from, Vector3 _to, float _radius, float _duration, int _generation)
    {
        GameObject projectile = GetProjectile();
        float throwSeconds = Mathf.Max(0.05f, assets.sporeBombThrowSeconds);

        if (null != projectile)
        {
            projectile.SetActive(true);
            SpriteRenderer renderer = projectile.GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            while (elapsed < throwSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / throwSeconds);
                Vector3 pos = Vector3.Lerp(_from, _to, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * assets.sporeBombArcHeight;
                projectile.transform.position = pos;
                projectile.transform.Rotate(0f, 0f, 540f * Time.deltaTime);
                if (null != renderer) renderer.sortingOrder = Order(pos) + 200;
                yield return null;

                if (_generation != sporeGeneration)
                {
                    projectile.SetActive(false);
                    yield break;
                }
            }

            projectile.SetActive(false);
        }

        if (null == vfx || _generation != sporeGeneration) yield break;

        vfx.PlaySporeExplosionVFX(_to, Vector2.up);
        Sound.Play(SoundID.SporeExplosion, _to);

        // 연막: 범위 안 무작위 지점에서 포자가 피어오른다(도트 피해 구간 표시).
        float interval = Mathf.Max(0.05f, assets.sporeZonePuffInterval);
        float zoneElapsed = 0f;
        while (zoneElapsed < _duration && _generation == sporeGeneration)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.1f, _radius);
            offset.y *= 0.5f;
            vfx.PlaySporeExplosionVFX(_to + (Vector3)offset, offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.up);

            yield return new WaitForSeconds(interval);
            zoneElapsed += interval;
        }
    }

    private GameObject GetProjectile()
    {
        if (null == assets.sporeBombSprite || null == host) return null;

        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (null != projectilePool[i] && false == projectilePool[i].activeSelf) return projectilePool[i];
        }

        GameObject go = new GameObject("RoguelikeSporeBomb");
        go.transform.SetParent(host.transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = assets.sporeBombSprite;
        renderer.sortingLayerName = "Objects";
        go.SetActive(false);
        projectilePool.Add(go);
        return go;
    }
}
