using System.Collections;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

public class Checker : MonoBehaviour
{
    public PlayerColor color; // Enum: White or Black
    public bool isSelected = false;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine moveCoroutine;

    /// <param name="animated">False for full board rebuild (e.g. sync from <c>GameState</c>) so checkers do not lerp from world origin.</param>
    public void MoveToPosition(Vector3 targetPosition, Transform newParent, bool animated = true)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = null;

        transform.SetParent(newParent);
        if (!animated)
        {
            transform.position = targetPosition;
            return;
        }

        moveCoroutine = StartCoroutine(AnimateMove(targetPosition));
    }

    private IEnumerator AnimateMove(Vector3 target)
    {
        Vector3 startPos = transform.position;
        float time = 0;
        float duration = Mathf.Max(0.05f, BackgammonSettings.MoveAnimDurationSeconds);

        float hopHeight = 0.5f * Mathf.Clamp(duration / 0.3f, 0.5f, 1.5f); 

        while (time < duration)
        {
            time += Time.deltaTime;
            float lerpFactor = moveCurve.Evaluate(time / duration);
            
            // Linear horizontal move + Arc for the vertical "hop"
            Vector3 currentPos = Vector3.Lerp(startPos, target, lerpFactor);
            currentPos.y += Mathf.Sin(lerpFactor * Mathf.PI) * hopHeight;
            
            transform.position = currentPos;
            yield return null;
        }

        transform.position = target;
        moveCoroutine = null;
    }

    public void SetVisualColor(Color newColor)
    {
        GetComponent<MeshRenderer>().material.color = newColor;
    }
}