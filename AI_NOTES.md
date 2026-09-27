# AI usage notes

Codex implemented this project in the existing Unity workspace. It was given the original Russian test task, the pasted production-quality implementation brief, the user's bootstrap constraints and Definition of Done, and the live project files. It inspected the installed editor, package manifest and lock, scene, Git state, and Unity Console before and during implementation. No other AI tool was used in this session.

Real user-supplied prompts and instructions used here include:

1. “Before modifying anything: inspect the actual installed Unity version; inspect Packages/manifest.json and packages-lock.json; do not replace VContainer with Zenject ...”
2. “The project is complete only when ... one Play session contains one server and two independent clients ... duplicated/retried shot executes once ... stale delayed shot cannot execute in a future turn ...”
3. From the supplied implementation brief: “Clients and server may communicate only through serialized messages transported through the transport abstraction.”

Errors found and corrected during this AI session:

- The first UI background was placed offscreen and the slider fills overlapped controls. A Game View capture exposed both; the layout was corrected.
- The first test asmdef duplicated Test Runner references. Unity compilation reported it; the redundant references were removed.
- The first reconnect loop would have reopened a deliberately silent break. Review exposed it; explicit Connect now gates that path.
- Moving runtime code into an asmdef left the scene component serialized with its old assembly name. The live editor reattached it and saved the corrected scene.
- A final lifecycle review found that the scene root needed to call `LifetimeScope.OnDestroy`; that call is now explicit.
- Adversarial review found that recreating a client with a shot packet still in flight lost the unconfirmed intent. The session store now retains that small outgoing command record, and a live delayed-packet check verified recovery.

Codex chose the protocol fields, server deadline check, command deduplication, sanitized snapshots, session storage, and tests. The user supplied the priorities and constraints. A developer submitting this project should review the code and be ready to explain or modify these choices live.

In a later user-directed refactor, Codex split runtime types into separate C# files, moved client timing, network slider limits, palette, logging, refresh and background settings to Inspector-authored configuration, removed fixed random seeds, and replaced runtime UI generation with `Main.unity` plus `ClientPanel.prefab` and `Cell.prefab`. A temporary Editor script using Unity's `PrefabUtility`, `SerializedObject`, and `EditorSceneManager` authored the assets in the live Unity Editor; that script is not part of the shipped game. The 12 EditMode tests passed after the change, and a Game View capture showed two connected clients with separate panels.

A further user-directed pass grouped runtime files by responsibility and namespace, made public DTO fields PascalCase, added braces to every control-flow body with a temporary Roslyn syntax rewriter, removed validation methods from `BattleshipConfig` and its child data classes, and removed unused return values and constants. Codex also made each client draw its four startup transport settings independently from the Inspector-authored maximums. A new EditMode test checks their independence and bounds; all 13 tests passed. Temporary syntax and Editor scripts were outside the Unity project and were removed after use.
