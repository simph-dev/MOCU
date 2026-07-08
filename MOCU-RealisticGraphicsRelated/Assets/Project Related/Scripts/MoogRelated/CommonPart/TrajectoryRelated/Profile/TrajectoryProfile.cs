using UnityEngine;

namespace MoogModule
{
    /// <summary>
    /// Specifies the motion profile that defines how the distance along a trajectory evolves over time,
    /// independently of the spatial shape of the path. While <see cref="TrajectoryType"/> determines the geometry
    /// of the path (e.g., linear, circular), <see cref="TrajectoryProfile"/> determines the timing of the movement
    /// along that path (e.g., uniform speed, acceleration curve).
    /// </summary>
    public enum TrajectoryProfile
    {
        None,
        Linear,
        CDF

        // Bezier
        // Smooth
        // ...
    }

    // todo: Algebraic Sigmoid
    /*/// <summary>
    /// Рациональная сигмоида (Algebraic Sigmoid)
    /// </summary>
    /// <param name="x">Значение от 0 до 1 (Progress (time))</param>
    /// <param name="k">Коэффициент кривизны (k=2.4 для аппроксимации 6 сигм)</param>
    public float AlgebraicSigmoid(float x, float k = 2.4f)
    {
        // 1. Ограничиваем входные данные, чтобы избежать отрицательных оснований 
        // для степени (Mathf.Pow выдаст NaN для отрицательных чисел с дробной степенью)
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;

        // 2. Считаем веса. При k > 0 значения x^k и (1-x)^k 
        // всегда будут в пределах [0, 1], что исключает переполнение.
        float xK = Mathf.Pow(x, k);
        float invXK = Mathf.Pow(1f - x, k);

        // 3. Финальный результат. Знаменатель никогда не будет равен 0, 
        // так как x и (1-x) не могут быть нулями одновременно.
        return xK / (xK + invXK);
    }*/
}