using UnityEngine;

[RequireComponent(typeof(Camera))] [ExecuteAlways]
public class ResponsiveCamera : MonoBehaviour
{
    public float targetVisibleWidth = 10f; // in world units

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        AdjustCamera();
    }

    #if UNITY_EDITOR
    private void Update()
    {
        AdjustCamera();
    }
    #endif

    private void AdjustCamera()
    {
        // Calculate the aspect ratio of the current screen (e.g., 9:16 = 0.5625)
        float currentAspectRatio = (float)Screen.width / Screen.height;

        // The formula for an orthographic camera is Width = 2 * Size * AspectRatio
        // We rearrange to solve for Size based on the width we want and the current aspect ratio:
        // Size = (Width / 2) / AspectRatio

        float sizeToSet = (targetVisibleWidth / 2f) / currentAspectRatio;

        cam.orthographicSize = sizeToSet;
    }
}