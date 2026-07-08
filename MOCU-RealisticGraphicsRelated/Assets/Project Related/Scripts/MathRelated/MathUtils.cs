public static class MathUtils
{
    public static float RandomSign() => UnityEngine.Random.value > 0.5f ? 1f : -1f;
    public static bool IsEven(int value) => value % 2 == 0;
    public static bool IsOdd(int value) => value % 2 != 0;
}