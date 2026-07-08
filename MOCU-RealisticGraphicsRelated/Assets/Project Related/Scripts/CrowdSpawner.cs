using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition; // Для HDRP

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CrowdSpawner : MonoBehaviour
{
    public enum LookMode { FaceCameraParallel, LookAtCameraPoint, LookAwayFromCamera, SameDirectionAsCamera }
    public enum RaceType { Western, Eastern }

    [System.Serializable]
    public class LinkedCellGroup
    {
        public string name = "Group";
        public Color groupColor = Color.cyan;
        public List<Vector2Int> cells = new List<Vector2Int>();
    }

    [Header("Warmup & Assets")]
    public ShaderVariantCollection warmupCollection;
    public RenderTexture warmupTexture; // 2D Array (16x16x2)
    public string warmupLayerName = "Warmup";

    [Header("Scene Settings")]
    public Transform crowdContainer;
    public Transform cameraTransform;

    [Header("Rotation")]
    public LookMode lookingDirection = LookMode.FaceCameraParallel;
    [Range(-180, 180)]
    public float modelRotationFix = 180f;

    [Header("Crowd Size")]
    [HideInInspector] public Vector2Int countRange = new Vector2Int(5, 7);

    [Header("Dimensions")]
    public float characterRadius = 0.17f;

    [Header("Jitter")]
    [Range(0f, 1f)] public float jitterStrength = 1.0f;

    [Header("Grid Settings")]
    public Transform gridCenter;
    public int gridWidthCount = 6;
    public int gridDepthCount = 4;
    public float cellSize = 1.0f;

    [Header("Blocking & Groups")]
    public LayerMask obstacleLayer;
    public List<Vector2Int> disabledCells = new List<Vector2Int>();
    public List<LinkedCellGroup> spawnGroups = new List<LinkedCellGroup>();

    [Header("Prefabs")]
    public List<GameObject> westernMalePrefabs;
    public List<GameObject> easternMalePrefabs;

    private class CharacterInstance
    {
        public GameObject GameObject;
        public Transform Transform;
        public RaceType Race;
    }

    private List<CharacterInstance> _pool = new List<CharacterInstance>();
    private List<CharacterInstance> _activeCrowd = new List<CharacterInstance>();
    private List<Vector2Int> _physicallyValidCells = new List<Vector2Int>();
    private HashSet<int> _registeredPrefabIDs = new HashSet<int>();
    private Camera _warmupCamera;

    System.Collections.IEnumerator Start()
    {
        if (crowdContainer == null) crowdContainer = new GameObject("Crowd_Pool").transform;
        _registeredPrefabIDs.Clear();

        // 1. Слой и шейдеры
        int warmupLayer = 6;
        //int warmupLayer = LayerMask.NameToLayer(warmupLayerName);
        if (warmupLayer == -1) { Debug.LogError("Слой WarmUp не найден!"); yield break; }
        if (warmupCollection != null) warmupCollection.WarmUp();

        // 2. Скрытая камера
        CreateWarmupCamera(warmupLayer);

        // 3. Пул
        Vector3 warmUpPos = cameraTransform.position + cameraTransform.forward * 2f;
        AddToPool(westernMalePrefabs, RaceType.Western, warmUpPos, warmupLayer);
        AddToPool(easternMalePrefabs, RaceType.Eastern, warmUpPos, warmupLayer);

        // 4. Прогрев VRAM
        foreach (var charInst in _pool) charInst.GameObject.SetActive(true);
        _warmupCamera.enabled = true;

        for (int i = 0; i < 15; i++)
        {
            _warmupCamera.Render(); // Принудительно заставляем камеру рисовать
            yield return null;
        }

        float timeout = 5f;
        while (timeout > 0 && ((ulong)Texture.streamingTextureLoadingCount + Texture.streamingMipmapUploadCount) > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        _warmupCamera.enabled = false;
        foreach (var charInst in _pool)
        {
            charInst.GameObject.SetActive(false);
            SetLayerRecursively(charInst.GameObject, 0);
            charInst.Transform.position = new Vector3(0, -500, 0);
        }

        Debug.Log($"Прогрев завершен. Загружено текстур: {Texture.streamingTextureLoadingCount}");

        BakeGrid();
    }

    void CreateWarmupCamera(int layer)
    {
        if (_warmupCamera != null) return;

        GameObject camObj = new GameObject("HiddenWarmupCamera");
        camObj.transform.SetParent(cameraTransform);
        camObj.transform.localPosition = Vector3.zero;

        _warmupCamera = camObj.AddComponent<Camera>();
        _warmupCamera.cullingMask = 1 << layer;
        _warmupCamera.targetTexture = warmupTexture;
        _warmupCamera.depth = -10;
        _warmupCamera.tag = "Untagged";
        _warmupCamera.enabled = false;

        var hdData = camObj.AddComponent<HDAdditionalCameraData>();
        hdData.xrRendering = false;
    }

    void AddToPool(List<GameObject> prefabs, RaceType race, Vector3 spawnPos, int layer = 0)
    {
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            int id = prefab.GetInstanceID();
            if (_registeredPrefabIDs.Contains(id)) continue;
            _registeredPrefabIDs.Add(id);
            GameObject obj = Instantiate(prefab, spawnPos, prefab.transform.rotation, crowdContainer);
            SetLayerRecursively(obj, layer);
            _pool.Add(new CharacterInstance { GameObject = obj, Transform = obj.transform, Race = race });
            obj.SetActive(false);
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, newLayer);
    }

    // Проверка, что клетка не отключена вручную и физически доступна
    bool IsCellValidForSpawn(Vector2Int cell)
    {
        if (disabledCells.Contains(cell)) return false;
        if (!_physicallyValidCells.Contains(cell)) return false;
        return true;
    }

    Vector3 GetLocalPosFromGrid(Vector2Int gridPos)
    {
        Vector3 startOffset = GetGridStartOffset();
        return startOffset + new Vector3(gridPos.x * cellSize, 0, gridPos.y * cellSize);
    }

    Vector3 GetGridStartOffset()
    {
        float totalWidth = gridWidthCount * cellSize;
        float totalDepth = gridDepthCount * cellSize;
        return new Vector3(-totalWidth / 2f + cellSize / 2f, 0, -totalDepth / 2f + cellSize / 2f);
    }

    public void BakeGrid()
    {
        _physicallyValidCells.Clear();
        if (gridCenter == null) return;
        Vector3 startOffset = GetGridStartOffset();
        for (int x = 0; x < gridWidthCount; x++)
            for (int z = 0; z < gridDepthCount; z++)
            {
                Vector3 worldPos = gridCenter.TransformPoint(startOffset + new Vector3(x * cellSize, 0, z * cellSize));
                if (!Physics.CheckBox(worldPos, Vector3.one * (cellSize / 2.1f), gridCenter.rotation, obstacleLayer))
                    _physicallyValidCells.Add(new Vector2Int(x, z));
            }
    }

    public void Hide()
    {
        foreach (var c in _activeCrowd) c.GameObject.SetActive(false);
        _activeCrowd.Clear();
    }

    public void ShowWestern() => StartNewTrial(RaceType.Western);
    public void ShowEastern() => StartNewTrial(RaceType.Eastern);

    public void StartNewTrial(RaceType targetRace)
    {
        Hide();
        BakeGrid(); // Обновляем данные о физических препятствиях

        // 1. Подготовка списков
        List<Vector2Int> cellsToSpawn = new List<Vector2Int>();
        HashSet<Vector2Int> usedCells = new HashSet<Vector2Int>();

        // 2. Обработка ГРУПП (Groups) - они имеют приоритет
        foreach (var group in spawnGroups)
        {
            if (group.cells.Count == 0) continue;

            // Фильтруем клетки группы (убираем те, что в стенах или отключены)
            var validGroupCells = group.cells.Where(c => IsCellValidForSpawn(c)).ToList();

            if (validGroupCells.Count > 0)
            {
                // Выбираем ОДНУ случайную клетку из группы
                Vector2Int winner = validGroupCells[Random.Range(0, validGroupCells.Count)];

                cellsToSpawn.Add(winner);

                // Помечаем все клетки группы как "использованные", чтобы туда больше никто не встал случайно
                foreach (var c in group.cells) usedCells.Add(c);
            }
        }

        // 3. Добивка случайными (если нужно)
        int targetTotalCount = Random.Range(countRange.x, countRange.y + 1);
        int neededMore = targetTotalCount - cellsToSpawn.Count;

        if (neededMore > 0)
        {
            // Берем все доступные, вычитаем те, что уже заняты группами
            var freeCells = _physicallyValidCells
                .Where(c => !disabledCells.Contains(c) && !usedCells.Contains(c))
                .ToList();

            Shuffle(freeCells);

            for (int i = 0; i < neededMore && i < freeCells.Count; i++)
            {
                cellsToSpawn.Add(freeCells[i]);
            }
        }

        // 4. Подбор персонажей
        var candidates = _pool.Where(p => p.Race == targetRace).ToList();
        Shuffle(candidates);

        int finalCount = Mathf.Min(cellsToSpawn.Count, candidates.Count);

        // 5. Расстановка
        float maxSafeOffset = Mathf.Max(0, (cellSize * 0.5f) - characterRadius);
        float actualJitterLimit = maxSafeOffset * jitterStrength;

        for (int i = 0; i < finalCount; i++)
        {
            var person = candidates[i];
            Vector2Int gridPos = cellsToSpawn[i];
            Vector3 localPos = GetLocalPosFromGrid(gridPos);

            Vector2 randomOffset = Random.insideUnitCircle * actualJitterLimit;
            Vector3 jitter3D = new Vector3(randomOffset.x, 0, randomOffset.y);

            person.Transform.position = gridCenter.TransformPoint(localPos + jitter3D);
            //ApplyRotation(person.Transform);

            person.GameObject.SetActive(true);
            _activeCrowd.Add(person);
        }
    }

    private void ApplyRotation(Transform personTransform)
    {
        if (cameraTransform == null) return;
        Vector3 lookDirection = Vector3.forward;

        switch (lookingDirection)
        {
            case LookMode.FaceCameraParallel: lookDirection = -cameraTransform.forward; break;
            case LookMode.LookAtCameraPoint: lookDirection = (cameraTransform.position - personTransform.position).normalized; break;
            case LookMode.LookAwayFromCamera: lookDirection = cameraTransform.forward; break;
            case LookMode.SameDirectionAsCamera: lookDirection = cameraTransform.forward; break;
        }

        lookDirection.y = 0;
        if (lookDirection != Vector3.zero) personTransform.rotation = Quaternion.LookRotation(lookDirection);
        personTransform.Rotate(0, modelRotationFix, 0);
    }

    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--; int k = Random.Range(0, n + 1);
            T value = list[k]; list[k] = list[n]; list[n] = value;
        }
    }

    private void OnDrawGizmos()
    {
        if (gridCenter == null) return;
        Vector3 startOffset = GetGridStartOffset();

#if UNITY_EDITOR
        GUIStyle textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.white;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.fontSize = 12;
        textStyle.fontStyle = FontStyle.Bold;
#endif

        // 1. Рисуем клетки
        for (int x = 0; x < gridWidthCount; x++)
        {
            for (int z = 0; z < gridDepthCount; z++)
            {
                Vector3 localPos = startOffset + new Vector3(x * cellSize, 0, z * cellSize);
                Vector3 worldPos = gridCenter.TransformPoint(localPos);
                Vector2Int currentCell = new Vector2Int(x, z);

                bool isManualDisabled = disabledCells.Contains(currentCell);
                bool isPhysBlocked = Physics.CheckBox(worldPos, Vector3.one * (cellSize / 2.1f), gridCenter.rotation, obstacleLayer);

                LinkedCellGroup myGroup = spawnGroups.FirstOrDefault(g => g.cells.Contains(currentCell));

                // Для Гизмо используем матрицу, чтобы кубики вращались вместе с объектом
                Gizmos.matrix = Matrix4x4.TRS(worldPos, gridCenter.rotation, Vector3.one);

                // --- ЦВЕТА ---
                Color fillColor;

                if (isManualDisabled) fillColor = new Color(0, 0, 0, 0.5f);
                else if (isPhysBlocked) fillColor = new Color(1, 0, 0, 0.4f);
                else if (myGroup != null)
                {
                    fillColor = myGroup.groupColor;
                    fillColor.a = 0.6f;
                }
                else fillColor = new Color(0, 1, 0, 0.15f);

                // Рисуем ПЛОСКУЮ заливку
                Gizmos.color = fillColor;
                Gizmos.DrawCube(Vector3.zero, new Vector3(cellSize * 0.95f, 0.05f, cellSize * 0.95f));

                // Сбрасываем матрицу Гизмо, чтобы Handles рисовались корректно в мировых координатах
                Gizmos.matrix = Matrix4x4.identity;

                // --- ВИЗУАЛИЗАЦИЯ СПАВНА (ПЛОСКОЕ КОЛЬЦО) ---
                if (!isManualDisabled && !isPhysBlocked)
                {
                    float maxSafeOffset = Mathf.Max(0, (cellSize * 0.5f) - characterRadius);
                    float displayJitter = maxSafeOffset * jitterStrength;

                    if (displayJitter > 0.01f)
                    {
#if UNITY_EDITOR
                        // Используем Handles для рисования плоского диска
                        Handles.color = new Color(1f, 0.9f, 0.2f, 0.8f); // Желтый яркий
                        // Рисуем диск в worldPos, нормаль смотрит вверх относительно gridCenter (учитывает наклон пола)
                        Handles.DrawWireDisc(worldPos, gridCenter.up, displayJitter);
#endif
                    }
                }

#if UNITY_EDITOR
                // Подписи координат
                if (!isManualDisabled && !isPhysBlocked)
                {
                    Handles.Label(worldPos, $"{x},{z}", textStyle);
                }
#endif
            }
        }

        // 2. Рисуем ЛИНИИ связей групп
        foreach (var group in spawnGroups)
        {
            if (group.cells.Count < 2) continue;

            Gizmos.color = group.groupColor;

            for (int i = 0; i < group.cells.Count; i++)
            {
                Vector3 p1Local = startOffset + new Vector3(group.cells[i].x * cellSize, 0, group.cells[i].y * cellSize);
                Vector3 p1 = gridCenter.TransformPoint(p1Local) + Vector3.up * 0.5f;

                Gizmos.DrawSphere(p1, 0.1f); // Узелок

                if (i < group.cells.Count - 1)
                {
                    Vector3 p2Local = startOffset + new Vector3(group.cells[i + 1].x * cellSize, 0, group.cells[i + 1].y * cellSize);
                    Vector3 p2 = gridCenter.TransformPoint(p2Local) + Vector3.up * 0.5f;
                    Gizmos.DrawLine(p1, p2);
                }
            }

            if (group.cells.Count > 2)
            {
                Vector3 pLastLocal = startOffset + new Vector3(group.cells.Last().x * cellSize, 0, group.cells.Last().y * cellSize);
                Vector3 pFirstLocal = startOffset + new Vector3(group.cells[0].x * cellSize, 0, group.cells[0].y * cellSize);

                Vector3 pLast = gridCenter.TransformPoint(pLastLocal) + Vector3.up * 0.5f;
                Vector3 pFirst = gridCenter.TransformPoint(pFirstLocal) + Vector3.up * 0.5f;

                Gizmos.DrawLine(pLast, pFirst);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CrowdSpawner))]
public class CrowdSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CrowdSpawner script = (CrowdSpawner)target;

        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Crowd Count Range (Applied AFTER Groups)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Groups always spawn 1 person each. If Range > Groups count, random people fill empty cells.", MessageType.Info);

        float minVal = script.countRange.x;
        float maxVal = script.countRange.y;

        EditorGUILayout.MinMaxSlider(ref minVal, ref maxVal, 0, 20);

        script.countRange = new Vector2Int(Mathf.RoundToInt(minVal), Mathf.RoundToInt(maxVal));

        CenterHtmlField($"Total Count: <b>{script.countRange.x}</b> to <b>{script.countRange.y}</b>");

        GUILayout.Space(20);
        GUI.backgroundColor = Color.green;

        if (GUILayout.Button("GENERATE NEW TRIAL", GUILayout.Height(40)))
        {
            Selection.activeGameObject = script.gameObject;
            script.BakeGrid();
            script.StartNewTrial(Random.value > 0.5f ? CrowdSpawner.RaceType.Western : CrowdSpawner.RaceType.Eastern);
            GUIUtility.ExitGUI();
        }
        GUI.backgroundColor = Color.white;
    }

    private void CenterHtmlField(string text)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
        EditorGUILayout.LabelField(text, style);
    }
}
#endif