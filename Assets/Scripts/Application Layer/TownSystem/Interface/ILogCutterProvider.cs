
using System;

// 제재소 가공 라인(최대 3개)의 커터를 UI에 라인 단위로 노출한다.
// 증설/철거나 세이브 로드로 활성 라인 수가 바뀌면 ActiveLineCountChangedEvent로 알린다.
public interface ILogCutterProvider
{
    public event Action<int> ActiveLineCountChangedEvent;
    public int ActiveLineCount { get; }
    public ILogCutter GetCutter(int _lineIdx);
}
