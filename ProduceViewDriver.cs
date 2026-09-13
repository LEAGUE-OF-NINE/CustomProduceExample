using System;
using UnityEngine;


// SPECIFIC TO THE CURRENT IMPLEMENTATION, NOT PART OF NORMAL PRODUCE LIFE-CYCLE (so you can delete if you want to)
public class ProduceViewDriver : MonoBehaviour
{
    public ProduceViewDriver(IntPtr ptr) : base(ptr) { }

    public System.Collections.Generic.List<Pair> pairs = new();

    public class Pair
    {
        public Transform view;
        public Transform pivot;

        public Vector3 viewStartLocalPos;
        public Vector3 viewStartLocalScale;
        public Vector3 pivotStartLocalPos;
        public Vector3 pivotStartLocalScale;
    }

    // LateUpdate so we run after the battle system's own per-frame
    // positioning (which typically happens in Update).
    private void LateUpdate()
    {
        if (pairs == null) return;

        for (int i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];
            if (p == null || p.view == null || p.pivot == null) continue;

            // Position: apply the pivot's delta on top of the view's start.
            var pivotDelta = p.pivot.localPosition - p.pivotStartLocalPos;
            p.view.localPosition = p.viewStartLocalPos + pivotDelta;

            // Scale: ratio so any non-unit view scale is preserved.
            float rx = SafeRatio(p.pivot.localScale.x, p.pivotStartLocalScale.x);
            float ry = SafeRatio(p.pivot.localScale.y, p.pivotStartLocalScale.y);
            float rz = SafeRatio(p.pivot.localScale.z, p.pivotStartLocalScale.z);
            
            p.view.localScale = new Vector3(
                p.viewStartLocalScale.x * rx,
                p.viewStartLocalScale.y * ry,
                p.viewStartLocalScale.z * rz);
        }
    }

    private static float SafeRatio(float current, float start)
    {
        if (Mathf.Approximately(start, 0f)) return 1f;
        return current / start;
    }

    public void Restore()
    {
        if (pairs == null) return;
        for (int i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];
            if (p == null || p.view == null) continue;
            p.view.localPosition = p.viewStartLocalPos;
            p.view.localScale = p.viewStartLocalScale;
        }
    }
}
