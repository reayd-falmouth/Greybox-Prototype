using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// Quadratic Bezier sampling for hover move-preview lines (testable, no scene required).
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
