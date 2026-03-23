using System.Collections.Generic;

public interface IInDungeonObjProvider
{
    public IReadOnlyList<ITreeObj> trees { get; }
}
