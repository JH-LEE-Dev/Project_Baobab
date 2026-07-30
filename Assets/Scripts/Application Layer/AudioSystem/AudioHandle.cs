// 재생 중인 특정 사운드 인스턴스를 가리키는 핸들.
// playId는 소스 슬롯이 다른 사운드로 강탈/재사용됐는지 검증하기 위한 세대 값이다.
public readonly struct AudioHandle
{
    public readonly int sourceIndex;
    public readonly int playId;

    public AudioHandle(int sourceIndex, int playId)
    {
        this.sourceIndex = sourceIndex;
        this.playId = playId;
    }

    public static readonly AudioHandle Invalid = new AudioHandle(-1, -1);

    public bool IsValid => sourceIndex >= 0 && playId >= 0;
}
