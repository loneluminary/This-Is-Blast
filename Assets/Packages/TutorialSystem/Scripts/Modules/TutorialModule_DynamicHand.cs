using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Utilities.Extensions;

namespace TUTORIAL_SYSTEM
{
    public class TutorialModule_DynamicHand : TutorialModule
    {
        [SerializeField, HideLabel][OnValueChanged("SetNormalHand")] private TutorialManager.TransformSpaceType TransformSpace;

        [SerializeField] float speed = 1;
        [SerializeField] float waitingTime = .5f;
        [SerializeField] float waitTimeForReplay = 1f;
        [SerializeField] bool hideBeforeReplay;

        public List<HandPointStruct> Points = new();

        [FoldoutGroup("Visuals")][SerializeField] Sprite normalHand;
        [FoldoutGroup("Visuals")][SerializeField] Sprite clickHand;
        [FoldoutGroup("Visuals")][SerializeField] SpriteRenderer hand;
        [FoldoutGroup("Visuals")][SerializeField] Image handImage;

        private Coroutine loopCoroutine;
        private static readonly WaitForSeconds _waitForSeconds_025 = new(0.25f);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Points.Count > 0 && Points[0].Point != null)
            {
                transform.position = Points[0].GetOffsetPosition(TransformSpace);
            }
        }
#endif

        public override IEnumerator ActivateModule()
        {
            if (loopCoroutine != null) StopCoroutine(loopCoroutine);
            loopCoroutine = StartCoroutine(StartLoop());

            yield return new WaitForEndOfFrame();
        }

        private IEnumerator StartLoop()
        {
            if (!ArePointsNullOrDisable()) transform.position = Points[0].GetOffsetPosition(TransformSpace);
            else yield break;

            Vector3 handScale = TransformSpace == TutorialManager.TransformSpaceType.ThreeD ? hand.transform.localScale : handImage.transform.localScale;
            if (hideBeforeReplay)
            {
                if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) hand.transform.localScale = Vector3.one * .001f;
                else handImage.transform.localScale = Vector3.one * .001f;

                if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) yield return hand.transform.DOScale(handScale, speed).WaitForCompletion();
                else yield return handImage.transform.DOScale(handScale, speed).WaitForCompletion();
            }

            SetNormalHand();

            while (true)
            {
                if (ArePointsNullOrDisable()) yield break;

                for (int i = 0; i < Points.Count; i++)
                {
                    yield return transform.DOMove(Points[i].GetOffsetPosition(TransformSpace), speed).SetEase(Ease.Linear).WaitForCompletion();

                    switch (Points[i].HandEventType)
                    {
                        case TutorialManager.HandEventType.Normal:
                            {
                                SetNormalHand();
                                break;
                            }
                        case TutorialManager.HandEventType.Holding:
                            {
                                if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) hand.sprite = clickHand;
                                else handImage.sprite = clickHand;
                                break;
                            }
                        case TutorialManager.HandEventType.Click:
                            {
                                if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) hand.sprite = clickHand;
                                else handImage.sprite = clickHand;
                                yield return _waitForSeconds_025;
                                SetNormalHand();
                                break;
                            }
                        case TutorialManager.HandEventType.DoubleClick:
                            {
                                if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) hand.sprite = clickHand;
                                else handImage.sprite = clickHand;
                                yield return _waitForSeconds_025;
                                SetNormalHand();
                                yield return new WaitForSeconds(.15f);
                                if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) hand.sprite = clickHand;
                                else handImage.sprite = clickHand;
                                yield return _waitForSeconds_025;
                                SetNormalHand();
                                break;
                            }
                        default:
                            break;
                    }

                    yield return new WaitForSeconds(waitingTime);
                }

                if (hideBeforeReplay)
                {
                    if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) yield return hand.transform.DOScale(handScale * .01f, speed).WaitForCompletion();
                    else yield return handImage.transform.DOScale(handScale * .01f, speed).WaitForCompletion();

                    transform.position = Points[0].GetOffsetPosition(TransformSpace);
                }

                SetNormalHand();

                yield return new WaitForSeconds(waitTimeForReplay);

                if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) hand.gameObject.SetActive(true);
                else handImage.gameObject.SetActive(true);

                if (hideBeforeReplay)
                {
                    if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD) yield return hand.transform.DOScale(handScale, speed).WaitForCompletion();
                    else yield return handImage.transform.DOScale(handScale, speed).WaitForCompletion();
                }
                else yield return transform.DOMove(Points[0].GetOffsetPosition(TransformSpace), speed).SetEase(Ease.Linear).WaitForCompletion();

                yield return new WaitForEndOfFrame();
            }
        }

        private void SetNormalHand()
        {
            if (TransformSpace == TutorialManager.TransformSpaceType.ThreeD)
            {
                hand.sprite = normalHand;
                handImage.sprite = null;
            }
            else
            {
                handImage.sprite = normalHand;
                hand.sprite = null;
            }
        }

        private bool ArePointsNullOrDisable()
        {
            Points.RemoveAll(point => point.Point == null);
            if (!Points.IsNullOrEmpty() && Points.TrueForAll(p => p.Point.gameObject.activeInHierarchy))
            {
                gameObject.SetActive(true);
                return false;
            }
            else
            {
                TutorialManager.Instance.DebugLog("Point list is null!", gameObject, TutorialManager.DebugType.Error);
                gameObject.SetActive(false);
                return true;
            }
        }

        [System.Serializable]
        public struct HandPointStruct
        {
            public Transform Point;
            [Tooltip("For UI (RectTransform): X,Y are percentages of rect size (0-1). For 3D: world space offset.")]
            public Vector3 Offset;
            public TutorialManager.HandEventType HandEventType;

            public Vector3 GetOffsetPosition(TutorialManager.TransformSpaceType handSpace)
            {
                if (Point == null) return Offset;

                if (Point is RectTransform rect)
                {
                    // Target is UI: Get the actual world corners of the RectTransform
                    Vector3[] corners = new Vector3[4];
                    rect.GetWorldCorners(corners);

                    // Calculate center position
                    Vector3 center = (corners[0] + corners[2]) / 2f;

                    // Calculate size in world space
                    float worldWidth = Vector3.Distance(corners[1], corners[2]);
                    float worldHeight = Vector3.Distance(corners[1], corners[0]);

                    // Apply percentage offset
                    return center + new Vector3(worldWidth * (Offset.x - 0.5f), worldHeight * (Offset.y - 0.5f), Offset.z);
                }

                // Target is a 3D Transform
                if (handSpace != TutorialManager.TransformSpaceType.ThreeD)
                {
                    // Hand is UI, but Target is 3D. Convert World to Screen Space.
                    if (Camera.main != null)
                    {
                        Vector3 screenPos = Camera.main.WorldToScreenPoint(Point.position + Offset);

                        // For Screen Space - Overlay canvases, the Z value isn't needed.
                        // If the target is behind the camera, you might want to hide the hand, 
                        // but for tutorials, the target is usually in view.
                        screenPos.z = 0;
                        return screenPos;
                    }
                    else
                    {
                        Debug.LogWarning("Dynamic Hand: No Main Camera found to convert 3D point to UI space.");
                    }
                }

                // Hand is 3D, Target is 3D
                return Point.position + Offset;
            }
        }
    }
}