# Battleship implementation plan

## Goal and baseline

Deliver one Unity 6 scene containing one authoritative server and two independent logical clients. The inspected project uses Unity 6000.3.24f1, VContainer 1.19.0, UniTask, and Unity Test Framework 1.6.0. The existing Main scene and package setup are retained.

## Architecture

- Pure C# board, fleet placement, and match rules own ship and turn state.
- A server runtime owns the match, player sessions, command deduplication, and authoritative turn deadline.
- An in-process transport sends serialized protocol bytes and independently simulates latency, jitter, loss, and duplication for each client.
- Two disposable client runtimes own separate projections, pending commands, connection health, and network settings.
- A per-slot in-memory session store retains only reconnect identity and command sequence across client recreation.
- Thin Unity scene adapters present both boards and debug controls. VContainer performs composition at the scene root.

## Protocol and guarantees

- Connect/resume requests carry an optional server-issued session token; the response carries a player-specific sanitized snapshot.
- Fire carries a stable command sequence, expected turn ID, and target cell. The server authenticates, validates, records a result, and deduplicates retries.
- Snapshots carry a monotonic revision and turn ID. Clients discard older revisions. They contain own ships and shots, but only known results for the enemy board.
- Heartbeats and repeated snapshot delivery detect silent connection loss and restore state after dropped packets.
- The server checks its deadline before processing a shot; a stale turn ID cannot execute in a later turn.
- All delayed work is cancelled on runtime or scene disposal.

## Stages and estimate

1. Domain rules, placement, and tests: 2 hours.
2. Server sessions, timing, and protocol tests: 3 hours.
3. Serialized unreliable transport and client convergence: 4 hours.
4. Reconnect, cold recreation, and lifetime handling: 3 hours.
5. One-scene UI, settings, logging, and manual chaos pass: 4 hours.
6. Risk tests, documentation, and final review: 3 hours.

Estimated total: 19 hours. Measure actual time in README at completion.

## Risks

Duplicate commands, delayed shots from old turns, stale snapshot rollback, enemy ship disclosure, lost acknowledgements, disconnect while pending, and callbacks after scene destruction. Tests will target these before optional bonuses.

## Deliberate simplifications

No real socket stack, server persistence, cryptographic authentication, event log, or bandwidth optimization. Ships may touch. Disconnect does not pause the match; a full scene restart begins a fresh match. Jitter provides natural reordering without a separate control.
