#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NaughtyAttributes;
using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

// Sentry 연동 검증용 임시 트리거. 검증 완료 후 제거할 것.
// Standalone 빌드(Development Build)에서는 Inspector 버튼을 누를 수 없으므로 F9/F10/F11 단축키로도 실행 가능하게 함.
public class SentryTestTrigger : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[Key.F9].wasPressedThisFrame)
        {
            SendTestMessage();
        }
        else if (Keyboard.current[Key.F10].wasPressedThisFrame)
        {
            ThrowTestException();
        }
        else if (Keyboard.current[Key.F11].wasPressedThisFrame)
        {
            LogTestError();
        }
    }

    [Button("Send Test Message (F9)")]
    private void SendTestMessage()
    {
        SentrySdk.CaptureMessage("Sentry test message from SentryTestTrigger");
        UnityEngine.Debug.Log("[SentryTestTrigger] Test message sent.");
    }

    [Button("Throw Test Exception (F10)")]
    private void ThrowTestException()
    {
        throw new System.Exception("Sentry test exception from SentryTestTrigger");
    }

    [Button("Log Test Error (F11)")]
    private void LogTestError()
    {
        UnityEngine.Debug.LogError("Test Debug.LogError from SentryTestTrigger (Sentry integration check)");
    }
}
#endif
