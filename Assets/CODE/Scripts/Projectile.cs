using System;
using DG.Tweening;
using Lean.Pool;
using UnityEngine;
using Utilities.Extensions;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float travelSpeed = 18f;
    [SerializeField] private float arcHeight = 0.5f;

    private GridBlock _target;
    private Action _onHitCallback;
    private Vector3 _startPos;
    private Tween _travelTween;

    public void Initialize(Vector3 startPosition, GridBlock target, ColorType color, Action onHit)
    {
        _startPos = startPosition;
        _target = target;
        _onHitCallback = onHit;

        transform.position = startPosition;

        StartTravelTween();
    }

    private void StartTravelTween()
    {
        if (_target == null)
        {
            OnReachTarget();
            return;
        }

        Vector3 endPos = _target.transform.position;

        _travelTween = DOTween.To(() => 0f, t => UpdateProjectilePosition(t), 1f, travelSpeed).SetSpeedBased(true).SetEase(Ease.Linear).OnComplete(OnReachTarget);
    }

    private void UpdateProjectilePosition(float t)
    {
        if (_target == null)
        {
            _travelTween?.Kill();
            OnReachTarget();
            return;
        }

        Vector3 endPos = _target.transform.position;
        Vector3 linear = Vector3.Lerp(_startPos, endPos, t);

        // Upward arc on Y-axis (parabolic)
        float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = linear + Vector3.up * arc;

        // Face travel direction
        Vector3 nextPos = Vector3.Lerp(_startPos, endPos, Mathf.Clamp01(t + 0.02f));
        Vector3 dir = (nextPos - transform.position).normalized;
        if (dir != Vector3.zero) transform.forward = dir;
    }

    private void OnReachTarget()
    {
        if (_target)
        {
            _target.TriggerDestroy();
            _onHitCallback?.Invoke();
        }

        if (this.TryGetComponentInChildren(out TrailRenderer trail)) trail.Clear();
        LeanPool.Despawn(gameObject);
    }

    private void OnDestroy() => _travelTween?.Kill();
    private void OnDisable() => _travelTween?.Kill();
}