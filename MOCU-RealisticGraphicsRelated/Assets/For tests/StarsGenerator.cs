/*using UnityEngine;


public class CubesGenerator : MonoBehaviour
{
    public GameObject cubePrefab;
    public GameObject parentObject;

    public int numberOfCubes = 69;
    public Vector3 minBounds = new Vector3(-5, -5, -5);
    public Vector3 maxBounds = new Vector3(5, 5, 5);


    void GenerateCubes()
    {
        for (int i = 0; i < numberOfCubes; i++)
        {
            float x = Random.Range(minBounds.x, maxBounds.x);
            float y = Random.Range(minBounds.y, maxBounds.y);
            float z = Random.Range(minBounds.z, maxBounds.z);
            Vector3 randomPosition = new Vector3(x, y, z);

            Quaternion randomRotation = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));

            GameObject cubeInstance = Instantiate(cubePrefab, randomPosition, randomRotation, parentObject.transform);
            cubeInstance.layer = parentObject.layer;
        }
    }
}*/

using UnityEngine;
using UnityEngine.Rendering;


public class StarsGenerator : MonoBehaviour
{
    private int starCount = 1250;
    private Vector3 bounds = new Vector3(1.3f, 1.3f, 1.0f);
    private Vector3 cloudCenter = new Vector3(0f, 1.7f, 0.66f);
    private Vector3 starScale = new Vector3(0.02f, 0.02f, 0.02f);

    private Mesh starMesh;
    public Material starMaterial; // Материал с включенным GPU Instancing

    // Массив для хранения всех трансформаций (позиция, вращение, масштаб)
    private Matrix4x4[] matrices;
    private bool isVisible = false;

    private void Awake()
    {
        matrices = new Matrix4x4[starCount];
        starMesh = CreateStarMesh();
        RandomizeStars();
    }

    public void RandomizeStars()
    {
        for (int i = 0; i < starCount; i++)
        {
            // Случайная позиция в заданных границах (это остаётся как было)
            Vector3 localPosition = new Vector3(
                Random.Range(-bounds.x / 2, bounds.x / 2),
                Random.Range(-bounds.y / 2, bounds.y / 2),
                Random.Range(-bounds.z / 2, bounds.z / 2)
            );

            // ✨ ПРИБАВЛЯЕМ СМЕЩЕНИЕ ЦЕНТРА
            Vector3 worldPosition = cloudCenter + localPosition;
            Quaternion rotation = Quaternion.Euler(0, 180, 0);

            /*Quaternion rotation = Quaternion.Euler(
                Random.Range(0, 360),
                Random.Range(0, 360),
                Random.Range(0, 360)
            );*/

            // Используем новую позицию в матрице
            matrices[i] = Matrix4x4.TRS(worldPosition, rotation, starScale);
        }
    }

    void Update()
    {
        // Если кубы видимы, рисуем их все одной командой
        if (isVisible && starCount > 0)
        {
            Graphics.DrawMeshInstanced(
                starMesh,
                0,
                starMaterial,
                matrices,
                starCount,
                null, // MaterialPropertyBlock - нам не нужен
                //ShadowCastingMode.Off,
                UnityEngine.Rendering.ShadowCastingMode.On, // Включить тени (стандартно)
                //false, // Принимать тени (стандартно)
                true, // Принимать тени (стандартно)
                gameObject.layer // ✨ ЗАДАЕМ НУЖНЫЙ СЛОЙ
            );
        }
    }

    public void Hide()
    {
        isVisible = false;
    }

    public void Show()
    {
        isVisible = true;
    }

    // .........

    private Mesh CreateStarMesh()
    {
        var mesh = new Mesh();

        // Вершины (точки) треугольника
        Vector3[] vertices = new Vector3[3]
        {
            new Vector3(0, 0.5f, 0),    // Верхняя точка
            new Vector3(-0.5f, -0.5f, 0), // Левая нижняя
            new Vector3(0.5f, -0.5f, 0)  // Правая нижняя
        };

        // Индексы вершин, которые образуют треугольник
        int[] triangles = new int[3] { 0, 1, 2 };

        // Нормали (чтобы освещение работало корректно)
        Vector3[] normals = new Vector3[3]
        {
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;

        return mesh;
    }
}