using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TUTORIAL_SYSTEM
{
    [ExecuteAlways]
    public class TutorialModule_CutOutMask : TutorialModule
    {
        public Image MaskObject, MaskFill;
        public Image[] ClickBlockers;

        [Range(1, 20)] public float HoleScale = 1;
        [Range(0, 1)] public float HoleRadius = .8f;
        public Color MaskColor;

        public Transform Target;  // Unified target (UI or World)
        public bool RaycastTarget = true;

        public override IEnumerator ActivateModule()
        {
            if (!Application.isPlaying) yield break;

            for (int i = 0; i < ClickBlockers.Length; i++)
            {
                ClickBlockers[i].raycastTarget = RaycastTarget;
            }

            DOTween.To(() => 100f, (x) => HoleScale = x, HoleScale, 1f).OnUpdate(UpdateData);
        }

#if UNITY_EDITOR
        private void Update() => UpdateData();
#endif
        private void UpdateData()
        {
            if (Target == null) return;
            if (ClickBlockers.Length < 4) return;

            float radius = Mathf.Clamp((1 - HoleRadius - .4f) * 10f, 0, 10f);
            MaskObject.pixelsPerUnitMultiplier = radius;
            MaskFill.color = MaskColor;

            if (Target.TryGetComponent<RectTransform>(out var targetRect) && Target.GetComponentInParent<Canvas>()) UpdateDataUI(targetRect);
            else UpdateDataWorld();
        }

        private void UpdateDataUI(RectTransform targetUI)
        {
            RectTransform maskRect = MaskObject.rectTransform;

            // Sync Mask properties to Target
            maskRect.anchorMax = targetUI.anchorMax;
            maskRect.anchorMin = targetUI.anchorMin;
            maskRect.pivot = targetUI.pivot;

            float padding = (HoleScale - 1) * 20f;
            maskRect.sizeDelta = targetUI.sizeDelta + (Vector2.one * padding);
            maskRect.position = targetUI.position;

            // Calculate hole dimensions in world space
            float worldWidth = maskRect.rect.width * maskRect.lossyScale.x;
            float worldHeight = maskRect.rect.height * maskRect.lossyScale.y;

            // Find the true center of the hole regardless of pivot
            Vector3 pivotOffset = new Vector3((0.5f - maskRect.pivot.x) * worldWidth, (0.5f - maskRect.pivot.y) * worldHeight, 0);
            Vector3 holeCenter = maskRect.position + pivotOffset;

            // Position blockers based on their actual sizes
            for (int i = 0; i < 4; i++)
            {
                RectTransform bRect = ClickBlockers[i].rectTransform;
                float bWidth = bRect.rect.width * bRect.lossyScale.x;
                float bHeight = bRect.rect.height * bRect.lossyScale.y;

                switch (i)
                {
                    case 0:
                        bRect.position = holeCenter + new Vector3(0, -(worldHeight * 0.5f + bHeight * 0.5f), 0);
                        break;
                    case 1:
                        bRect.position = holeCenter + new Vector3(0, worldHeight * 0.5f + bHeight * 0.5f, 0);
                        break;
                    case 2:
                        bRect.position = holeCenter + new Vector3(worldWidth * 0.5f + bWidth * 0.5f, 0, 0);
                        break;
                    case 3:
                        bRect.position = holeCenter + new Vector3(-(worldWidth * 0.5f + bWidth * 0.5f), 0, 0);
                        break;
                }
            }
        }

        private void UpdateDataWorld()
        {
            if (Camera.main == null) return;

            RectTransform maskRect = MaskObject.rectTransform;

            // Convert 3D World Position to 2D Screen Position
            Vector3 screenPos = Camera.main.WorldToScreenPoint(Target.position);

            // If the object is behind the camera, we shouldn't draw the mask hole over it
            if (screenPos.z < 0) return;

            screenPos.z = 0; // Flatten the Z axis for the UI Canvas
            maskRect.position = screenPos;

            // Set the Size of the hole for the 3D object
            // 3D scale doesn't equal UI pixels. We multiply by 100 as a baseline so the hole isn't 1 pixel wide.
            float baseSizeX = Target.lossyScale.x * 100f;
            float baseSizeY = Target.lossyScale.y * 100f;

            float padding = (HoleScale - 1) * 20f;
            maskRect.sizeDelta = new Vector2(baseSizeX, baseSizeY) + (Vector2.one * padding);

            // Position the Blockers (Using the exact same reliable logic you used for UI)
            float worldWidth = maskRect.rect.width * maskRect.lossyScale.x;
            float worldHeight = maskRect.rect.height * maskRect.lossyScale.y;

            // Assuming a standard centered pivot (0.5, 0.5) for the 3D target hole
            Vector3 pivotOffset = new Vector3((0.5f - maskRect.pivot.x) * worldWidth, (0.5f - maskRect.pivot.y) * worldHeight, 0);
            Vector3 holeCenter = maskRect.position + pivotOffset;

            for (int i = 0; i < 4; i++)
            {
                RectTransform bRect = ClickBlockers[i].rectTransform;
                float bWidth = bRect.rect.width * bRect.lossyScale.x;
                float bHeight = bRect.rect.height * bRect.lossyScale.y;

                switch (i)
                {
                    case 0:
                        bRect.position = holeCenter + new Vector3(0, -(worldHeight * 0.5f + bHeight * 0.5f), 0);
                        break;
                    case 1:
                        bRect.position = holeCenter + new Vector3(0, worldHeight * 0.5f + bHeight * 0.5f, 0);
                        break;
                    case 2:
                        bRect.position = holeCenter + new Vector3(worldWidth * 0.5f + bWidth * 0.5f, 0, 0);
                        break;
                    case 3:
                        bRect.position = holeCenter + new Vector3(-(worldWidth * 0.5f + bWidth * 0.5f), 0, 0);
                        break;
                }
            }
        }
    }
}