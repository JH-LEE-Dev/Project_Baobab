using UnityEngine;

/// <summary>
/// 연출이 바뀔 때 지워진 파티클 연출의 남은 알갱이가 수명대로 사라질 때까지 두었다가 오브젝트를 지운다(StageAmbienceFade.TryRetire).
/// </summary>
public class StageAmbienceRetiredParticles : MonoBehaviour
{
    // 알갱이가 이보다 오래 남으면 그냥 지운다.
    private const float MaxSeconds = 15f;

    private ParticleSystem[] systems;
    private float elapsed;

    public void Setup(ParticleSystem[] _systems)
    {
        systems = _systems;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < MaxSeconds && null != systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (null != systems[i] && systems[i].particleCount > 0)
                {
                    return;
                }
            }
        }

        Destroy(gameObject);
    }
}
