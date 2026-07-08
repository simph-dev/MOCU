using System.Collections.Generic;
using UnityEngine;


namespace RaceExperiment
{
    public class RaceScene : MonoBehaviour
    {
        public List<GameObject> WesternModels;
        public List<GameObject> EasternModels;

        private GameObject _currentModel;

        //private float _nextActionTime = 0f;

        private void Awake()
        {
            foreach (var model in WesternModels)
                model?.SetActive(false);

            foreach (var model in EasternModels)
                model?.SetActive(false);
        }

        private void Update() { }

        public void ShowWesternModel()
        {
            ShowRandomFromList(WesternModels);
        }

        public void ShowEasternModel()
        {
            ShowRandomFromList(EasternModels);
        }

        public void HideModel()
        {
            //Debug.Log("Hided model");
            _currentModel?.SetActive(false);
            _currentModel = null;
        }

        private void ShowRandomFromList(List<GameObject> modelList)
        {
            HideModel();

            if (modelList == null || modelList.Count == 0)
            {
                Debug.LogWarning("Список моделей пуст или не назначен.");
                return;
            }

            int index = Random.Range(0, modelList.Count);
            _currentModel = modelList[index];
            _currentModel?.SetActive(true);
        }

        private void ShowRandomModel()
        {
            var allModels = new List<GameObject>();
            allModels.AddRange(WesternModels);
            allModels.AddRange(EasternModels);

            if (allModels.Count == 0)
                return;

            int index = Random.Range(0, allModels.Count);
            var selected = allModels[index];

            if (selected != null)
            {
                HideModel();
                selected.SetActive(true);
                _currentModel = selected;
            }
        }
    }
}