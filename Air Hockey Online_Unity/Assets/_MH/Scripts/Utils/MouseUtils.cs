using MH.Core;
using UnityEngine;

public static class MouseUtils
{
    public static float GetMouseWorldDistanceFromScreenCenter(Camera cam=null)
    {
        if (cam == null)
            cam = Camera.main;

        // Screen positions
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        Vector3 mouseScreenPos = Input.mousePosition;

        // Choose a depth from the camera to project to (for 2D, usually cam.nearClipPlane or a fixed z)
        screenCenter.z = cam.nearClipPlane;
        mouseScreenPos.z = cam.nearClipPlane;

        // Convert to world space
        Vector3 centerWorld = cam.ScreenToWorldPoint(screenCenter);
        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreenPos);

        // Distance in Unity units
        return Vector3.Distance(mouseWorld, centerWorld);
    }

    public static Vector3 GetMouseWorldPosition(Camera cam = null, float zFromCamera = 0f)
    {
        if (cam == null)
            cam = Camera.main;
        Vector3 mouseScreenPos = Input.mousePosition;
        // For 2D (orthographic), you can usually ignore zFromCamera and just use cam.nearClipPlane
        mouseScreenPos.z = zFromCamera <= 0f ? cam.nearClipPlane : zFromCamera;
        return cam.ScreenToWorldPoint(mouseScreenPos);
    } 
}