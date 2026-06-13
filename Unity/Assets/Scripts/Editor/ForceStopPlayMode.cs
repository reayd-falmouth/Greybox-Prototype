using UnityEditor;
using UnityEngine;

public class ForceStopPlayMode
{
    [MenuItem("Tools/Force Stop Play Mode")]
    public static void StopPlayMode()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.Log("[ForceStop] Stopping Play Mode...");
            EditorApplication.isPlaying = false;
        }
        else
        {
            Debug.Log("[ForceStop] Editor is not in Play Mode.");
        }
    }
}
