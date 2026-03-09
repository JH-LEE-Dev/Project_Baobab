using UnityEngine;

public interface IEnvironmentProvider
{
    public IShadowDataProvider shadowDataProvider { get; }
    public IGroundDataProvider groundDataProvider { get; }
    public ITilemapDataProvider tilemapDataProvider { get; }
}
