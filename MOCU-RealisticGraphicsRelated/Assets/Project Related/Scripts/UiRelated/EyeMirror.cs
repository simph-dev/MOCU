using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

// Required by [SerializeReference], which is how CustomPassVolume stores its passes.
// Without it the pass's fields - Target Texture in particular - are not reliably
// persisted across scene reloads and domain reloads.
[System.Serializable]
public class EyeMirror : CustomPass
{
    public enum Eye
    {
        Left = 0,
        Right = 1
    }

    public RenderTexture targetTexture;

    /// Which eye is copied. Both are rendered in one pass (single-pass instanced),
    /// into the two slices of one texture array, and a plain blit into a flat
    /// texture lets both eyes write it - whichever lands last shows. So the slice is
    /// picked explicitly, and every instance of the blit copies that one.
    public Eye eye = Eye.Left;

    private RTHandle m_TargetHandle;
    private MaterialPropertyBlock m_Properties;
    private int m_BilinearPass = -1;

    private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
    private static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
    private static readonly int BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
    private static readonly int BlitTexArraySlice = Shader.PropertyToID("_BlitTexArraySlice");

    protected override void Execute(CustomPassContext ctx)
    {
        // 1. СТРОЖАЙШАЯ ПРОВЕРКА: Копируем только из VR камеры
        // Если это камера монитора или вармапа — выходим немедленно
        if (ctx.hdCamera.camera.name.Contains("Monitor") ||
            ctx.hdCamera.camera.name.Contains("Warmup") ||
            ctx.hdCamera.camera.targetTexture != null) // VR камера обычно рендерит в null (экран)
            return;

        if (targetTexture == null) return;

        if (m_TargetHandle == null || m_TargetHandle.rt != targetTexture)
        {
            m_TargetHandle?.Release();
            m_TargetHandle = RTHandles.Alloc(targetTexture);
        }

        RTHandle source = ctx.cameraColorBuffer;
        Material singleSlice = Blitter.GetBlitMaterial(TextureDimension.Tex2DArray, singleSlice: true);

        bool eyesInArray = source.rt != null
                           && source.rt.dimension == TextureDimension.Tex2DArray
                           && source.rt.volumeDepth > (int)eye
                           && singleSlice != null
                           && singleSlice.IsKeywordEnabled("BLIT_SINGLE_SLICE");

        // No array of eyes to pick from - no XR, or one eye per pass: copied as
        // before.
        if (!eyesInArray)
        {
            HDUtils.BlitCameraTexture(ctx.cmd, source, m_TargetHandle, 0);
            return;
        }

        if (m_BilinearPass < 0)
            m_BilinearPass = singleSlice.FindPass("Bilinear");

        m_Properties ??= new MaterialPropertyBlock();

        Vector2 scale = source.useScaling ? (Vector2)source.rtHandleProperties.rtHandleScale : Vector2.one;
        m_Properties.SetTexture(BlitTexture, source);
        m_Properties.SetVector(BlitScaleBias, new Vector4(scale.x, scale.y, 0f, 0f));
        m_Properties.SetFloat(BlitMipLevel, 0f);
        m_Properties.SetInt(BlitTexArraySlice, (int)eye);

        CoreUtils.SetRenderTarget(ctx.cmd, m_TargetHandle);
        ctx.cmd.DrawProcedural(Matrix4x4.identity, singleSlice, m_BilinearPass, MeshTopology.Triangles, 3, 1, m_Properties);
    }

    protected override void Cleanup() => m_TargetHandle?.Release();
}
