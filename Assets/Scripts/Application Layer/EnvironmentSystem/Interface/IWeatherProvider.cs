using UnityEngine;

public interface IWeatherProvider 
{
    public WeatherType GetCurrentWeatherType();
}
