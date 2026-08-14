using UnityEngine;

public static class Sound
{
    public static void Play(SoundID id, Vector3 position, float volume = 1f, bool is3D = true, float pitchOverride = -1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioEvent e = new AudioEvent(id, position, volume, is3D, pitchOverride);
        AudioManager.Instance.EnqueueEvent(e);
    }

    // bypassDucking: 이 재생만 UI 믹서 그룹으로 보내 덕킹/일시정지 음소거를 받지 않게 한다.
    // 같은 SoundID를 게임플레이와 UI 연출이 함께 쓰는 경우에만 필요하다 - 예를 들어 GetItem은
    // 인벤토리에서는 게임플레이 효과음이라 그대로 덕킹을 받아야 하지만, 결과창의 카운트업
    // 연출에서는 그 창 자신의 피드백이라 자기가 건 덕킹에 먹먹해지면 안 된다.
    public static void PlayUI(SoundID id, float volume = 1f, float pitchOverride = -1f, bool bypassDucking = false)
    {
        if (AudioManager.Instance == null)
            return;

        AudioEvent e = new AudioEvent(id, Vector3.zero, volume, false, pitchOverride, bypassDucking);
        AudioManager.Instance.EnqueueEvent(e);
    }

    public static void PlayBGM(SoundID id, float volume = 1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayBGM(id, volume);
    }

    // 지정한 시간에 걸쳐 볼륨을 낮추며 BGM을 정지한다.
    public static void FadeOutBGM(float duration)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.FadeOutBGM(duration);
    }

    // 루프 사운드 등, 재생 이후에도 정지/위치 갱신이 필요한 경우 핸들을 통해 제어한다.
    public static AudioHandle PlayTracked(SoundID id, Vector3 position, float volume = 1f, bool is3D = true, float pitchOverride = -1f)
    {
        if (AudioManager.Instance == null)
            return AudioHandle.Invalid;

        return AudioManager.Instance.PlayTracked(id, position, volume, is3D, pitchOverride);
    }

    public static void StopTracked(AudioHandle handle)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.StopTracked(handle);
    }

    // 트랙 중인 사운드의 현재 피치를 조회한다(다른 사운드로 전환하며 피치를 이어받을 때 사용).
    public static float GetTrackedPitch(AudioHandle handle)
    {
        if (AudioManager.Instance == null)
            return 1f;

        return AudioManager.Instance.GetTrackedPitch(handle);
    }

    // 정지음이 없는 루프 사운드를 피치/볼륨을 서서히 낮추며(전원 꺼지듯) 정지한다.
    public static void StopTrackedWithPowerDown(AudioHandle handle, float duration = 0.4f, float minPitch = 0.1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.StopTrackedWithPowerDown(handle, duration, minPitch);
    }

    // 예열음이 없는 시작 사운드를 낮은 피치에서 목표 피치로 서서히 올리며(전원 들어오듯) 재생한다.
    public static AudioHandle PlayTrackedWithPowerUp(SoundID id, Vector3 position, float volume = 1f, bool is3D = true, float duration = 0.4f, float minPitch = 0.1f, float targetPitch = 1f)
    {
        if (AudioManager.Instance == null)
            return AudioHandle.Invalid;

        return AudioManager.Instance.PlayTrackedWithPowerUp(id, position, volume, is3D, duration, minPitch, targetPitch);
    }

    public static void UpdateTrackedPosition(AudioHandle handle, Vector3 position)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.UpdateTrackedPosition(handle, position);
    }

    // 이미 재생 중인 트랙 사운드의 피치를 현재 값에서 targetPitch까지 duration에 걸쳐 서서히 올리거나 내린다.
    public static void RampTrackedPitch(AudioHandle handle, float targetPitch, float duration)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.RampTrackedPitch(handle, targetPitch, duration);
    }

    // 이미 재생 중인 트랙 사운드의 볼륨 배율을 현재 값에서 targetVolumeScale까지 duration에 걸쳐 서서히 바꾼다.
    public static void RampTrackedVolume(AudioHandle handle, float targetVolumeScale, float duration)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.RampTrackedVolume(handle, targetVolumeScale, duration);
    }

    // 사운드 ID에 연결된 클립의 길이(초)를 조회한다.
    public static float GetClipLength(SoundID id)
    {
        if (AudioManager.Instance == null)
            return 0f;

        return AudioManager.Instance.GetClipLength(id);
    }

    // 현재 재생 중인 모든 3D 사운드를 즉시 정지하고 대기 큐를 비운다.
    public static void StopAll3DSounds()
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.StopAll3DSounds();
    }

    // 카메라 연출 등에 맞춰 3D 사운드 전역 계수를 즉시 설정한다 (0f~1f).
    public static void SetProduction3DVolumeFactor(float factor)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetProduction3DVolumeFactor(factor);
    }

    // 카메라 연출 등에 맞춰 3D 사운드 전역 계수를 지정한 시간에 걸쳐 서서히 페이드한다.
    public static void RampProduction3DVolume(float targetFactor, float duration)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.RampProduction3DVolume(targetFactor, duration);
    }

    // 핸들이 가리키는 소스가 지금 실제로 재생 중인지 확인한다(StopAll3DSounds 등 핸들을 무효화하지
    // 않고 AudioSource만 직접 정지시키는 경로가 있어, 루프를 계속 유지해야 하는 발음체는 이걸로
    // "핸들은 유효한데 소리는 멈춰있는" 상태를 감지해 다시 재생을 트리거해야 한다).
    public static bool IsTrackedPlaying(AudioHandle handle)
    {
        if (AudioManager.Instance == null)
            return false;

        return AudioManager.Instance.IsTrackedPlaying(handle);
    }

    // 트랙 중인 사운드의 볼륨/피치를 즉시(보간 없이) 설정한다. 호출부가 매 프레임 자체적으로
    // 목표값을 계산해 미는 경우(예: 컨베이어 벨트 속도 연동 루프 사운드)에 사용한다.
    public static void SetTrackedVolume(AudioHandle handle, float volume)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetTrackedVolume(handle, volume);
    }

    public static void SetTrackedPitch(AudioHandle handle, float pitch)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetTrackedPitch(handle, pitch);
    }

    // ResultUI/TentUI/EscUI/WarningUI/Navigation UI 등 대상 UI가 열려있는 동안 BGM/SFX/Ambience에
    // Low Pass Filter를 걸어 먹먹하게 만든다. 참조 카운트 방식이라 여러 대상 UI가 겹쳐 떠 있어도
    // 마지막 하나가 닫힐 때만 원복된다.
    public static void RequestAudioDuck()
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.RequestAudioDuck();
    }

    public static void ReleaseAudioDuck()
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.ReleaseAudioDuck();
    }

    // 일시정지(ESC 표시) 중 게임플레이 사운드(SFX/Ambience)를 통째로 음소거해 BGM과 UI 조작음만
    // 남긴다. 이미 울리고 있던 원샷도 함께 사라진다.
    public static void SetGameplayAudioMuted(bool muted)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetGameplayAudioMuted(muted);
    }

    // 옵션 창이 열려 있는 동안 덕킹/일시정지 음소거를 잠시 풀어, 조절 중인 볼륨을 실제로 들을 수
    // 있게 한다. (옵션은 ESC 메뉴에서 열리는데 그때는 게임플레이 사운드가 꺼져 있다)
    public static void SetAudioPreviewMode(bool enabled)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetAudioPreviewMode(enabled);
    }

    // 캐릭터 피로도(스태미나) 비율(0~1)에 맞춰 BGM/SFX에 긴박감 Low Pass Filter를 건다.
    // 던전 안에서 매 프레임 현재 비율로 호출하는 것을 전제로 한다. 던전을 벗어나면 1f로 호출해 해제한다.
    public static void SetFatigueRatio(float ratio)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetFatigueRatio(ratio);
    }
}
