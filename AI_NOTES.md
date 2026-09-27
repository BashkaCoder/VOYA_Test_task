# AI usage notes

The developer used Codex together with Unity CLI and Unity AI_Assistant. They first formulated and refined the task prompt, then brought it into the existing project and repeatedly reviewed and corrected the result until satisfied. The implementation was an iterative collaboration. Specific Unity AI_Assistant outputs were not recorded separately, so this document does not attribute individual code changes to that tool.

## Tools and context

Codex received the original Russian assignment, the expanded implementation brief, the existing-project constraints, the Definition of Done, and successive user code reviews. It inspected the installed Unity version, Packages/manifest.json, packages-lock.json, assembly definitions, source, scene/assets, Git history and Console. Existing VContainer, UniTask and Unity Test Framework dependencies were retained.

Codex used filesystem and Git tools for source changes and history, and Unity CLI to compile, run EditMode tests, inspect the live Editor, exercise Play Mode and capture Game View. Scene and prefab authoring used Unity Editor APIs through temporary CLI scripts; those authoring scripts are not shipped as runtime scene-generation code. A temporary Roslyn rewriter helped apply braces consistently. Configuration serialization changes were migrated and saved through the Editor.

## Three real prompts from the review

1. “do not replace VContainer with Zenject; do not introduce another DI framework; use the already installed UniTask; use the installed Unity Test Framework”
2. “Убери этот говнокод и поработай действительно со сценой/ассетами, создай префабы, сохрани сцену и тд”
3. “Сейчас все скрипты скинуты в одну кучу в папке "Runtime" - разбей их осмысленно на категории/подкатегории в соответствующие папки и неймспейсы.”

These were actual user instructions, not reconstructed examples. Later reviews also required independent randomized network conditions, Inspector-authored configuration, removal of excessive validation and unused code, and compact serialized auto-properties.

## Where AI was wrong and how it was caught

- The first implementation built UI at runtime in BattleshipScene.BuildUi. The developer identified this as hard to inspect and maintain. It was replaced with a saved scene and reusable prefabs.
- AI initially left too much code in one Runtime folder, hardcoded tunable values/seeds, and unnecessary validation and boilerplate. The developer's successive code reviews identified these issues and drove the configuration, responsibility, namespace and style changes.
- Game View inspection exposed an offscreen background and overlapping slider fills; the layout was corrected.
- Unity compilation exposed duplicate Test Runner references in an early test assembly definition; the redundant references were removed.
- Lifecycle review found that recreation could lose an unconfirmed shot still in flight. SessionStore now retains that command, and tests cover recovery with the same command ID. Review also caught a reconnect path that could undo an intentional silent disconnect.
- The earlier AI_NOTES incorrectly claimed that no other AI tool was used. The developer explicitly corrected the tool history to include Unity AI_Assistant; this version records that distinction.

## Decisions made by the developer

The developer defined the scope, acceptance criteria, priorities and existing dependency constraints. They chose an authoritative server with unreliable simulated delivery as the assignment's focus, required authored scene/prefab UI and Inspector configuration, and directed the code style, module separation, responsibility boundaries, removal of unnecessary checks and compact property syntax. They reviewed the generated implementation repeatedly and decided when it was ready to push.

Codex proposed and implemented the detailed protocol, command deduplication, turn IDs, revision filtering, sanitized snapshots, session storage and test cases within those constraints. Those implementation details are AI-assisted work and remain the submitting developer's responsibility to understand and explain. The test results establish the covered behaviors; they do not replace human review.

## Final verification

After the serialized auto-property migration, Unity 6000.3.24f1 compiled with zero errors and warnings, and all 13 EditMode tests passed. Earlier live Play checks covered two clients, randomized independent network settings, rapid clicking, silent disconnect/reconnect, recreation, packets in flight during scene restart, and victory. README distinguishes elapsed calendar time from untracked focused working time.