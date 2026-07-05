using UnityEngine;

public static class UIAudioHelper
{
    public static void PlayClickSfx(AudioClip clip, Transform fallbackTransform)
    {
        if (clip == null)
        {
            return;
        }

        Vector3 playPosition = Camera.main != null ? Camera.main.transform.position : (fallbackTransform != null ? fallbackTransform.position : Vector3.zero);
        AudioSource.PlayClipAtPoint(clip, playPosition);
    }
}