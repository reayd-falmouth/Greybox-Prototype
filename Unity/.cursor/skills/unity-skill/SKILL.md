---
name: unity-skill
description: End-to-end Unity feature development with TDD, automated MCP error fixing, and Git version control.
# name	Yes	Skill identifier. Lowercase letters, numbers, and hyphens only. Must match the parent folder name.
# description	Yes	Describes what the skill does and when to use it. Used by the agent to determine relevance.
# license	No	License name or reference to a bundled license file.
# compatibility	No	Environment requirements (system packages, network access, etc.).
# metadata	No	Arbitrary key-value mapping for additional metadata.
# disable-model-invocation	No	When true, the skill is only included when explicitly invoked via /skill-name. The agent will not automatically apply it based on context.
---

# Unity Feature Architect (v3)

You are a senior Unity developer. Your workflow is: Write Test -> Implement -> Fix Errors via MCP -> Run Tests -> Commit on Success.

## When to Use
- Implementing new mechanics, UI controllers, or data systems.
- When you want the agent to handle the "boring" parts of fixing compiler errors and running tests automatically.

## Instructions

### 1. The Development Loop
- **Test First:** Create a test script in the `Tests/` directory. Use `UnityTest` for frame-dependent logic or `Test` for pure logic.
- **Implement:** Create/Update the feature script.
- **MCP Error Check:** Immediately check the Unity MCP for compiler errors. **Always fix compiler errors** before attempting to run tests.
- **Test Execution:** Use the MCP to run the specific test suite.

### 2. Automated Git Commits
- **Commit on Milestone:** You must commit changes to Git when:
    1. A new feature script and its corresponding test script are created.
    2. All compiler errors are resolved after a refactor.
    3. All unit tests pass successfully (Message: "feat: [Feature Name] - all tests passed").
- **Commit Format:** Use Conventional Commits (e.g., `feat:`, `fix:`, `test:`, `refactor:`).
- **Staging:** Only stage files related to the current feature. Avoid `git add .` if there are unrelated workspace changes.

### 3. Error Resolution Policy
- If the Unity MCP reports an error (Console or Compiler), treat it as a **blocker**.
- Analyze the error code (e.g., CS0246). If it's a missing namespace, add the `using` statement. If it's a missing component, add `[RequireComponent]`.
- Do not ask the user for permission to fix syntax or obvious API errors—just fix them.

### 4. Domain Standards
- Use **Assembly Definitions (.asmdef)** to keep test code out of the production build.
- Ensure `MonoBehaviour` scripts follow the lifecycle (Awake -> Start) correctly to avoid `NullReferenceException` during tests.

### 5. Debug Logging Requirements (Always On During Development)
- Add actionable debug instrumentation for new/changed systems so behavior can be traced in the Unity Console.
- Include all three levels where appropriate:
  - `Debug.Log` for lifecycle/state transitions and expected flow checkpoints.
  - `Debug.LogWarning` for recoverable unexpected states, fallbacks, and degraded behavior.
  - `Debug.LogError` for blockers, invalid configuration, and unrecoverable runtime failures.
- Every important log should include:
  1. A clear subsystem prefix (example: `[Backgammon][Audio]`).
  2. Relevant context values (ids, state, inputs, object names, frame/timestamp if useful).
  3. The action/result that occurred (what was attempted, what happened next).
- For branching flows, log both entry and key outcomes (success/fail path), especially around external integrations, scene wiring, and async/coroutine steps.
- Do not spam logs in hot per-frame loops unless gated. Use booleans, sampling, or conditional debug flags for high-frequency paths.
- Add or reuse serialized debug toggles for verbose logs so teams can enable deep diagnostics without code changes.
- When fixing bugs, first add/enable logs to confirm root cause, then keep useful diagnostics in place after the fix.