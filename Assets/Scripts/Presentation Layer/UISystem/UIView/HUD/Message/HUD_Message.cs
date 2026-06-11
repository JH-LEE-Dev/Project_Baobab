using UnityEngine;
using System.Collections;

public class HUD_Message : MonoBehaviour
{
    private Coroutine hideCoroutine;

    public void Initialize()
    {
        Hide();
    }

    public void ShowForSeconds(float _duration)
    {
        gameObject.SetActive(true);

        if (null != hideCoroutine)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay(_duration));
    }

    public void Hide()
    {
        if (null != hideCoroutine)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator HideAfterDelay(float _duration)
    {
        yield return new WaitForSeconds(_duration);

        hideCoroutine = null;
        gameObject.SetActive(false);
    }
}
