using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    //외부 의존성
    private IUnitLogicProvider unitLogicProvider;

    //내부 의존성
    [SerializeField] private ParticleSystem rainEffectPrefab;
    private ParticleSystem rainEffect;

    private float weatherTimer;
    private float maxRainEmission;
    private float currentEmission;
    private float targetEmission;
    private float transitionSpeed = 15.0f; // Emission 변화 속도

    private WeatherType currentWeatherType;
    private GUIStyle debugStyle;

    public void Initialize(IUnitLogicProvider _unitLogicProvider)
    {
        rainEffect = Instantiate(rainEffectPrefab, transform);
        var rainPos = transform.position;
        rainPos.y += 55;
        rainEffect.transform.position = rainPos;

        unitLogicProvider = _unitLogicProvider;
        
        if (rainEffect != null)
        {
            maxRainEmission = rainEffect.emission.rateOverTime.constant;
            var emission = rainEffect.emission;
            emission.rateOverTime = 0f;
            currentEmission = 0f;
            targetEmission = 0f;
            rainEffect.Play();
        }

        currentWeatherType = WeatherType.Normal;
        weatherTimer = Random.Range(90f, 150f);
    }

    private void Update()
    {
        UpdateWeatherTimer();
        UpdateRainEmission();
        //UpdateRainPosition();
    }

    private void UpdateWeatherTimer()
    {
        weatherTimer -= Time.deltaTime;
        if (weatherTimer <= 0)
        {
            ChangeWeather();
            weatherTimer = Random.Range(90f, 150f);
        }
    }

    private void ChangeWeather()
    {
        currentWeatherType = (currentWeatherType == WeatherType.Normal) ? WeatherType.Rain : WeatherType.Normal;
        targetEmission = (currentWeatherType == WeatherType.Rain) ? maxRainEmission : 0f;
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

    private void OnGUI()
    {
        if (debugStyle == null)
        {
            debugStyle = new GUIStyle();
            debugStyle.fontSize = 12;
            debugStyle.normal.textColor = Color.white;
            debugStyle.alignment = TextAnchor.UpperRight;
        }

        string weatherInfo = $"Current Weather: {currentWeatherType}\nNext Change In: {weatherTimer:F1}s";
        float width = 300f;
        float height = 100f;
        GUI.Label(new Rect(Screen.width - width - 10f, 10, width, height), weatherInfo, debugStyle);
    }
}
