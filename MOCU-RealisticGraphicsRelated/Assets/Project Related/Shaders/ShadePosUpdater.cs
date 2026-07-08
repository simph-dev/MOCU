using UnityEngine;

[ExecuteAlways] // Чтобы работало даже в режиме редактирования
public class ShaderPosUpdater : MonoBehaviour
{
    public Material mat;

    void Update()
    {
        if (mat != null)
        {
            // Передаем координаты объекта (камеры), на котором висит скрипт
            mat.SetVector("_PlayerPos", transform.position);
        }
    }
}