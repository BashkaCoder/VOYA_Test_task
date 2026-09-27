# Network Battleship

Unity 6.3.24f1 project for the two-client Battleship test. Open `Assets/_Project/Scenes/Main.unity` and press Play. The Main scene is enabled in Build Settings. One Play session creates one server and two independent clients; no second build is needed.

## Architecture in 12 lines

1. `BattleshipScene` is the VContainer scene composition root and thin uGUI adapter.
2. `BattleshipConfig` holds the authored board, fleet, timer, and default network settings.
3. `FleetPlacement`, `Board`, and `Match` implement deterministic C# game rules.
4. The server creates and owns both secret boards and all turn state.
5. `BattleshipServer` serializes timeout and shot decisions on the Unity main thread.
6. The server constructs a different sanitized `Snapshot` for each player.
7. `SimulatedTransport` converts protocol DTOs to JSON UTF-8 bytes before scheduling delivery.
8. It independently applies each client's latency, jitter, loss, and duplication settings.
9. `ClientRuntime` owns one client's connection, local snapshot, and pending command.
10. The two runtimes cannot read each other's projections or the server's match.
11. `SessionStore` keeps each slot's token, next sequence, and any unconfirmed outgoing shot across recreation.
12. The UI displays only client projections and sends click intent through that client's runtime.

## Rules and controls

The default board is 6×6 with ships 3, 2, 2, 1 and a 15-second turn. Ships may touch but cannot overlap. A hit still passes the turn. A final hit on a ship is `Sunk`; sinking the whole fleet wins. The own board shows ships and incoming shots; the target board shows only results of your shots. The server keeps timing and validation authority.

Each panel has separate latency, jitter, loss, and duplicate sliders plus Disconnect, Connect, and Recreate client. Disconnect is silent: transport packets stop with no disconnect callback. The global controls restart the scene and toggle the network log. Jitter can reorder packets naturally. Routine heartbeats and unchanged snapshots are suppressed in the visible log so shot and drop events remain readable.

## Protocol

All messages use a small explicit `Packet` DTO serialized with Unity `JsonUtility` to UTF-8 bytes before transport delivery. Server and client parse bytes independently.

| Message | Direction | Key fields and purpose |
| --- | --- | --- |
| `Connect` | Client → server | Slot and optional stored session token; starts or resumes a session. |
| `Welcome` | Server → client | Server-issued token and current player-specific snapshot. |
| `Rejected` | Server → client | Explicit invalid-session reason. |
| `Heartbeat` | Client → server | Probes liveness and requests current state. |
| `Snapshot` | Server → client | Revisioned sanitized player view. |
| `Fire` | Client → server | Stable sequence, expected turn ID, target cell. |
| `Result` | Server → client | Accepted/rejected result, reason or `Miss`/`Hit`/`Sunk`, and snapshot. |

The token maps a recreated client back to its player slot. A `Fire` sequence is the command ID within that token's session. The server caches each command outcome and returns it on a duplicate or retry without mutating the match again. `ExpectedTurnId` prevents an old delayed shot from firing when the same player gets a later turn. The server checks its deadline before accepting a shot. Every state mutation increments `ServerRevision`, and a client discards snapshots with an older revision.

## Loss, reconnect, and lifecycle

The client sends a pending shot immediately, blocks another click for that turn, and retries the **same** command every 0.7 seconds until confirmed or superseded by authoritative state. Heartbeats run every second. After four seconds without a server message the client shows `Disconnected`; a deliberately broken link stays broken until Connect is pressed. A 100% loss profile can also trigger the watchdog, and the client retries connection while that link remains enabled. Once delivery returns, `Welcome` or heartbeat snapshots restore the current server view. The server keeps running and expiring absent players' turns; there is no forfeit policy.

Recreate client disposes its old runtime and transport subscription, then constructs a runtime with no snapshot. Only its token, sequence, and unconfirmed outgoing command survive in `SessionStore`; the new snapshot comes from the server. If the turn is still current, that command is retried with the same ID. If the turn has advanced, the player sees whether the shot took effect or did not execute. Scheduled packets are canceled on scene disposal and old client generations cannot receive packets after recreation. Restart Scene creates a fresh match and fresh in-memory session store.

## Tests and verification

Run EditMode tests in Unity Test Runner or with `unity command run_tests --mode editor`. Twelve EditMode tests cover placement, hit/sunk/victory, turn alternation, out-of-turn and duplicate shots, server timeout before late delivery, stale turn IDs, serialized view privacy, revision rollback, snapshot convergence, cold client state, and pending shot recovery.

All 12 tests passed on Unity 6000.3.24f1. Play Mode checks in the live editor confirmed both clients connected, rapid second fire was blocked, a silent break was detected, reconnect caught up to the server revision, recreation returned to the same match, a pending shot survived recreation and executed once, and scene reload with a 2-second delayed packet produced no Console error. A scripted full Play Mode match reached victory for A with server revision 22 and both clients at revision 22. A high-loss full match and demo video have not been recorded in this session.

## Decisions and limits

The simulated transport is intentionally in-process; there is no socket framework. JSON and full 6×6 snapshots favor inspectability and convergence over bandwidth. Server state and reconnect tokens are in memory only. Tokens distinguish local debug sessions, not production cryptographic identities. There is no server restart recovery, client prediction, or separate reorder knob. The same scene works without importing any new DI, async, or test framework; it uses the installed VContainer, UniTask, and Unity Test Framework.

`PLAN.md` forecast 19 hours for a conventional implementation. The Codex-assisted work in this session took approximately 25 minutes; the earlier Unity project bootstrap was already present and is excluded from that figure. The main deviations from the plan were generating the minimal uGUI panel at runtime from the existing Canvas, persisting an unconfirmed outgoing command for recreation, and reattaching the scene root after moving code into an asmdef. The live editor's background pause also required enabling `Application.runInBackground` for unattended verification.
