using System;
using UnityEngine;

public class WeatherManager : MonoBehaviour,IWeatherProvider
{
    public event Action<WeatherType> WeatherChagnedEvent;
    //외부 의존성
    private IUnitLogicProvider unitLogicProvider;

    //내부 의존성
    [Tooltip("날씨(비) 연출 사용 여부. 꺼두면 비 프리팹을 아예 생성하지 않습니다. " +
             "되살리려면 이 값을 켜고, 아래 주석 처리된 Update 호출들을 복구하세요.")]
    [SerializeField] private bool useWeatherEffect = false;

    [SerializeField] private ParticleSystem rainEffectPrefab;
    private ParticleSystem rainEffect;

    private float weatherTimer;
    private float maxRainEmission;
    private float currentEmission;
    private float targetEmission;
    private float transitionSpeed = 25.0f; // Emission 변화 속도

    private WeatherType currentWeatherType;

    public void Initialize(IUnitLogicProvider _unitLogicProvider)
    {
        unitLogicProvider = _unitLogicProvider;

        // 기능이 꺼져 있으면 프리팹을 생성조차 하지 않는다.
        //
        // 예전에는 여기서 무조건 Instantiate + Play를 하고 루트의 rateOverTime만 0으로 눌렀는데,
        // Rain Effect 프리팹은 오브젝트가 둘("Rain Effect" 1750개 / "Rain Splash" 1000개)이고
        // ParticleSystem.Play()는 기본이 withChildren:true라, 꺼놓은 기능인데도 자식 스플래시가
        // 초당 10개씩 계속 방출되고 있었다. 루트도 방출량만 0일 뿐 재생 중이라 매 프레임 갱신됐다.
        if (true == useWeatherEffect && null != rainEffectPrefab)
        {
            rainEffect = Instantiate(rainEffectPrefab, transform);

            var rainPos = transform.position;
            rainPos.y += 55;
            rainEffect.transform.position = rainPos;

            maxRainEmission = rainEffect.emission.rateOverTime.constant;
            var emission = rainEffect.emission;
            emission.rateOverTime = 0f;
            currentEmission = 0f;
            targetEmission = 0f;
            rainEffect.Play();
        }

        // 연출을 안 쓰더라도 이 값은 IWeatherProvider.GetCurrentWeatherType()의 반환값이므로
        // 항상 초기화해 둔다.
        currentWeatherType = WeatherType.Normal;
        weatherTimer = UnityEngine.Random.Range(90f, 150f);
    }

    // Update()는 호출할 내용이 전부 비활성이라 제거했다. 빈 Update도 프레임마다 콜백 비용이 붙는다.
    // 날씨를 되살릴 때 아래 UpdateWeatherTimer/UpdateRainEmission/UpdateRainPosition을 부르는
    // Update()를 다시 만들면 된다.

    private void UpdateWeatherTimer()
    {
        weatherTimer -= Time.deltaTime;
        if (weatherTimer <= 0)
        {
            ChangeWeather();
        }
    }

    private void ChangeWeather()
    {
        currentWeatherType = (currentWeatherType == WeatherType.Normal) ? WeatherType.Rain : WeatherType.Normal;
        targetEmission = (currentWeatherType == WeatherType.Rain) ? maxRainEmission : 0f;
        
        // 날씨가 변경될 때마다 새로운 타이머 설정
        weatherTimer = UnityEngine.Random.Range(90f, 150f);

        WeatherChagnedEvent?.Invoke(currentWeatherType);
    }

    private void UpdateRainEmission()
    {
        if (rainEffect == null) return;

        if (!Mathf.Approximately(currentEmission, targetEmission))
        {
            currentEmission = Mathf.MoveTowards(currentEmission, targetEmission, transitionSpeed * Time.deltaTime);
            var emission = rainEffect.emission;
            emission.rateOverTime = currentEmission;
        }
    }

    private void UpdateRainPosition()
    {
        if (rainEffect == null || unitLogicProvider == null) return;

        Transform playerTransform = unitLogicProvider.GetCharacterTransform();
        if (playerTransform != null)
        {
            Vector3 targetPos = playerTransform.position;
            targetPos.y += 15f;
            rainEffect.transform.position = targetPos;
        }
    }

    public WeatherType GetCurrentWeatherType()
    {
        return currentWeatherType;
    }
}
