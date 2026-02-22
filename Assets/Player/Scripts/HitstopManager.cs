using UnityEngine;
using System.Collections;

public class HitstopManager : MonoBehaviour
{
    public static HitstopManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void DoHitstop(float duration)
    {
        StartCoroutine(HitstopRoutine(duration));
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}
