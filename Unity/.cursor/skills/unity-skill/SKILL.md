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

# Unity Feature Architect (v2)

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