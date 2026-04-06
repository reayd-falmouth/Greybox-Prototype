using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// Quadratic Bezier sampling for hover move-preview lines (testable, no scene required).
    /// Includes arc control-point logic ported from MoneySession <c>MoveVisualizer.BuildArc</c>
    /// (perpendicular offset ∝ chord length, alternating side, spread by fan index).
    /// </summary>
    public static class BackgammonMovePreviewCurve
    {
        public static Vector3 GetControlPoint(Vector3 start, Vector3 end, float arcHeightWorld, float lateralWorld)
        {
            Vector3 mid = (start + end) * 0.5f;
            Vector3 chord = end - start;
            Vector3 perp = chord.sqrMagnitude > 1e-8f
                ? Vector3.Cross(Vector3.up, chord).normalized
                : Vector3.right;
            return mid + Vector3.up * arcHeightWorld + perp * lateralWorld;
        }

        /// <summary>
        /// Control point matching <c>MoveVisualizer.BuildArc</c>: midpoint plus in-plane perpendicular
        /// offset with magnitude <c>chordLength * heightFactor</c>, alternating side by <paramref name="fanIndex"/>,
        /// and spread <c>1 + fanIndex/2</c> (integer division).
        /// </summary>
        /// <param name="planeNormal">Board normal; perpendicular is <c>Cross(planeNormal, chordDir)</c>.</param>
        public static Vector3 GetMoveVisualizerStyleControlPoint(
            Vector3 p0,
            Vector3 p2,
            float heightFactor,
            int fanIndex,
            Vector3 planeNormal)
        {
            Vector3 chord = p2 - p0;
            float dist = chord.magnitude;
            if (dist < 1e-8f)
                return (p0 + p2) * 0.5f;

            Vector3 dir = chord / dist;
            Vector3 mid = (p0 + p2) * 0.5f;
            Vector3 perp = Vector3.Cross(planeNormal, dir);
            if (perp.sqrMagnitude < 1e-8f)
                perp = Vector3.Cross(Vector3.right, dir);
            perp.Normalize();

            float height = dist * heightFactor;
            float direction = (fanIndex % 2 == 0) ? 1f : -1f;
            float spread = 1f + fanIndex / 2;
            return mid + perp * height * direction * spread;
        }

        /// <summary>
        /// Samples the quadratic Bezier defined by <see cref="GetMoveVisualizerStyleControlPoint"/>.
        /// </summary>
        /// <returns>Number of positions written (segmentCount + 1), or 0 if the buffer is too small.</returns>
        public static int FillMoveVisualizerStyleBezier(
            Vector3 p0,
            Vector3 p2,
            float heightFactor,
            int segmentCount,
            int fanIndex,
            Vector3 planeNormal,
            Vector3[] outPositions)
        {
            Vector3 control = GetMoveVisualizerStyleControlPoint(p0, p2, heightFactor, fanIndex, planeNormal);
            return FillQuadraticBezier(p0, control, p2, outPositions, segmentCount);
        }

        /// <summary>
        /// Writes a quadratic Bezier P0→P1(control)→P2 into <paramref name="outPositions"/>.
        /// </summary>
        /// <returns>Number of positions written (segmentCount + 1), or 0 if the buffer is too small.</returns>
        public static int FillQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, Vector3[] outPositions, int segmentCount)
        {
            int n = Mathf.Clamp(segmentCount, 2, 128);
            if (outPositions == null || outPositions.Length < n + 1)
                return 0;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float u = 1f - t;
                outPositions[i] = u * u * start + 2f * u * t * control + t * t * end;
            }

            return n + 1;
        }
    }
}
