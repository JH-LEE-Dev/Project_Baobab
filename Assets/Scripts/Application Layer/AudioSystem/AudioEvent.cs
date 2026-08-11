using UnityEngine;

public struct AudioEvent
{
    public SoundID soundId;
    public Vector3 position;
    public float volume;
    public bool is3D;
    // 0f 미만이면 오버라이드 없음(AudioData/Cue 기본 피치 사용)
    public float pitchOverride;
    // 이 재생만 UI 믹서 그룹으로 보내 덕킹/일시정지 음소거를 받지 않게 한다.
    // 같은 SoundID라도 호출하는 쪽에 따라 성격이 다른 경우에 쓴다 - 예를 들어 GetItem은
    // 인벤토리에서 울릴 땐 게임플레이 효과음이지만, 결과창의 카운트업 연출에서 울릴 땐
    // 그 창 자신의 UI 피드백이라 자기가 건 덕킹에 먹먹해지면 안 된다.
    public bool bypassDucking;

    public AudioEvent(SoundID soundId, Vector3 position, float volume = 1f, bool is3D = true, float pitchOverride = -1f, bool bypassDucking = false)
    {
        this.soundId = soundId;
        this.position = position;
        this.volume = volume;
        this.is3D = is3D;
        this.pitchOverride = pitchOverride;
        this.bypassDucking = bypassDucking;
    }
}
