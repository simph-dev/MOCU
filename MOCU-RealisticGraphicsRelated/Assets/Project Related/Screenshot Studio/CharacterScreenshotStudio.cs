using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CharacterScreenshotStudio : MonoBehaviour
{
    [Header("Camera References")]
    public Camera camFullBody;
    public Camera camPortraitFront;
    public Camera camPortraitSide;

    [Header("Character Settings")]
    public List<GameObject> characters;
    public string folderName = "CharacterScreenshots";

    [Header("Capture Settings")]
    public float waitTimePerCharacter = 1.0f;

    public void StartBatchCapture()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Batch capture requires Play Mode to render HDRP effects correctly.");
            return;
        }

        StartCoroutine(CaptureProcessRoutine());
    }

    private IEnumerator CaptureProcessRoutine()
    {
        // Define directory path (one level above Assets)
        string directoryPath = Path.Combine(Application.dataPath, "..", folderName);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Initially hide all characters in the list
        foreach (GameObject character in characters)
        {
            if (character != null) character.SetActive(false);
        }

        Debug.Log("<color=orange>Character Screenshot Studio: Starting process...</color>");

        for (int i = 0; i < characters.Count; i++)
        {
            GameObject currentCharacter = characters[i];
            if (currentCharacter == null) continue;

            // Enable character and wait for systems to stabilize (Auto-exposure, etc.)
            currentCharacter.SetActive(true);

            // Wait for 1 second as requested for maximum stability
            yield return new WaitForSeconds(waitTimePerCharacter);

            // Capture the entire Game View (the collage of 3 cameras)
            string fileName = $"{currentCharacter.name}_Snapshot.png";
            string fullPath = Path.Combine(directoryPath, fileName);
            ScreenCapture.CaptureScreenshot(fullPath);

            Debug.Log($"<color=cyan>Captured {i + 1}/{characters.Count}:</color> {fileName}");

            // Extra buffer time for file I/O
            yield return new WaitForSeconds(0.2f);

            currentCharacter.SetActive(false);
        }

        Debug.Log($"<color=green>Process complete. Files saved to:</color> {directoryPath}");
    }
}

// --- EDITOR BUTTON LOGIC ---
#if UNITY_EDITOR
[CustomEditor(typeof(CharacterScreenshotStudio))]
public class CharacterScreenshotStudioEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CharacterScreenshotStudio studio = (CharacterScreenshotStudio)target;

        GUILayout.Space(20);
        GUI.backgroundColor = Color.green;

        if (GUILayout.Button("Capture All Screenshots", GUILayout.Height(40)))
        {
            studio.StartBatchCapture();
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif