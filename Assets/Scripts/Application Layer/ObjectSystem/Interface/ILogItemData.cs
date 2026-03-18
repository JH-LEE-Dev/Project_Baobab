using UnityEngine;

public interface ILogItemData : IItemData
{
    public LogState logState { get; }
    public TreeType treeType { get; }
    public Sprite sprite { get; }
}
