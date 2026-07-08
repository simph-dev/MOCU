using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

public class EyeMirror : CustomPass
{
    public RenderTexture targetTexture;
    private RTHandle m_TargetHandle;

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

        // Копируем левый глаз. Метод BlitCameraTexture сам поймет, 
        // что источник - это массив (VR), а цель - 2D текстура
        HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, m_TargetHandle, 0);
    }

    protected override void Cleanup() => m_TargetHandle?.Release();
}