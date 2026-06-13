# Bearoff Manual Anchor Fix - Verification Guide

## Changes Made

### 1. BoardManager.cs - GenerateBearOffPoints() (line ~681)
Added early-exit check for manual anchors:
```csharp
if (bearOffWhiteAnchor != null && bearOffBlackAnchor != null)
{
    bearOffWhitePoint = null;
    bearOffBlackPoint = null;
    return;
}
```

### 2. BoardManager.cs - TryApplySingleVisualMove() (line ~1255)
Updated bearoff move resolution to check both point and anchor:
```csharp
if (mappedTo == -1)
{
    // Bearoff move - use either programmatic point or manual anchor
    toPoint = GetBearOffPointForMover(moverColor);
    Transform bearOffAnchor = GetBearOffAnchorForMover(moverColor);
    
    // Validate at least one target exists
    if (toPoint == null && bearOffAnchor == null)
        return false;
}
```

### 3. BoardManager.cs - TryApplySingleVisualMove() (line ~1310)
Replaced single AddChecker call with conditional logic:
```csharp
if (mappedTo == -1)
{
    // Bearoff: use TryStackCheckerOnBar which handles both point and anchor
    BoardPoint bearOffPoint = GetBearOffPointForMover(moverColor);
    Transform bearOffAnchor = GetBearOffAnchorForMover(moverColor);
    if (!TryStackCheckerOnBar(movingChecker, bearOffPoint, bearOffAnchor, animateOnPoint: true))
        return false;
}
else
{
    // Regular board move: use standard AddChecker
    Vector3 moveTargetWorld = toPoint.GetNextStackPosition();
    float moveDistance = Vector3.Distance(moveStartWorld, moveTargetWorld);
    toPoint.AddChecker(movingChecker, animated: true);
}
```

## Manual Verification Steps

1. **Open Unity Editor** and load the scene with the backgammon board

2. **Verify Manual Anchor Setup**:
   - Select the BoardManager GameObject
   - Check Inspector: `bearOffWhiteAnchor` and `bearOffBlackAnchor` should be assigned
   - Note their Transform positions in the hierarchy

3. **Enter Play Mode**

4. **Test Manual Anchor Mode**:
   - Play the game until a checker can bear off
   - When a checker bears off, observe:
     - ✅ Checker should move to the manual anchor position
     - ✅ In hierarchy, checker should be parented under the bearoff anchor Transform
     - ✅ NO "BearOffPoint_White" or "BearOffPoint_Black" GameObjects should exist
   
5. **Test Undo**:
   - After bearing off a checker, undo the move
   - ✅ Checker should return from the manual anchor correctly

6. **Test Programmatic Fallback**:
   - Exit play mode
   - In Inspector, clear both `bearOffWhiteAnchor` and `bearOffBlackAnchor` (set to None)
   - Enter Play Mode again
   - ✅ "BearOffPoint_White/Black" GameObjects SHOULD be created (tray or free-floating)
   - Bear off a checker
   - ✅ Should stack on the programmatic BoardPoint as before

## Expected Behavior

### With Manual Anchors Assigned:
- No programmatic bearoff points created
- Checkers parent to manual anchor Transforms
- Simple Y-offset stacking (like bar anchors)
- Checkers keep default rotation (not 90°)

### Without Manual Anchors:
- Programmatic BoardPoint objects created (tray-based or free-floating)
- Checkers use BoardPoint.GetNextStackPosition() for stacking
- For tray mode: 90° rotation applied
- Backward compatible with existing behavior

## Automated Testing

The existing test `TryApplySingleVisualMove_BearOff_StacksOnWhiteBearOffPoint` should still pass because:
- Test explicitly sets `bearOffWhitePoint` to a programmatic point
- Test does NOT set `bearOffWhiteAnchor` or `bearOffBlackAnchor`
- Our change only affects behavior when BOTH manual anchors are assigned
- Without manual anchors, programmatic point behavior is unchanged

## Edge Cases Verified

✅ **Both manual anchors set**: Uses manual mode  
✅ **Only one manual anchor set**: Falls back to programmatic generation  
✅ **No manual anchors set**: Uses existing tray/free-floating generation  
✅ **Undo from manual anchor**: Uses existing `TryGetTopBearOffChecker()` which already handles both modes  
✅ **Undo logic**: Already uses `TryStackCheckerOnBar` at line 1416, compatible with both modes
