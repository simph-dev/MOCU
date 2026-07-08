using UnityEngine;

// For NON HDRP
[RequireComponent(typeof(Camera))]
public class CopyCameraView : MonoBehaviour
{
    public RenderTexture targetTexture;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // To VR
        Graphics.Blit(source, destination);

        // To UI
        if (targetTexture != null)
            Graphics.Blit(source, targetTexture);
    }
}