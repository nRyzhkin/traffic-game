# Traffic Game

A browser game about building road networks and fixing traffic, inspired by Cities: Skylines.
Draw roads, interchanges and roundabouts, watch an agent-based traffic simulation stress-test them,
solve traffic puzzles and recreate familiar city maps in the level editor.

**Platform:** WebGL (desktop and mobile browsers, landscape and portrait), published through PrimeSDK.
**Engine:** Unity 6000.5.2f1, URP 2D, Input System, UI Toolkit, JSON localization (ru, en, pt-BR, de, es, tr).

## Architecture

Model-View-Presenter with a single composition root.

| Assembly | Responsibility | Depends on |
|---|---|---|
| `TrafficGame.Core` | Road network, lane graph, traffic simulation, routing, levels, commands — plain C# | Unity.Mathematics only |
| `TrafficGame.Presentation` | Presenters and view interfaces — plain C# | Core |
| `TrafficGame.Views` | MonoBehaviours implementing view interfaces: procedural meshes, input, UI | Core, Presentation |
| `TrafficGame.App` | `GameBootstrap` composition root, configuration, platform services (PrimeSDK) | all of the above |
| `TrafficGame.Editor` | Reproducible project setup and scene generation | App, Views |

Core and Presentation contain no engine code, so all game logic is covered by fast EditMode tests;
`AssemblyBoundaryTests` keeps it that way.

## Getting started

1. Open the project with Unity 6000.5.2f1 (sign in to Unity Hub so registry packages can be downloaded).
2. Run **Traffic Game → Apply Project Setup** and **Traffic Game → Rebuild Game Scene**.
3. Open `Assets/_Project/Scenes/Game.unity` and press Play.
   Pan with the right or middle mouse button (or one finger), zoom with the wheel (or pinch).

Tests: **Window → General → Test Runner → EditMode**.

## Project layout

```
Assets/_Project/
  Scripts/{Core,Presentation,Views,App,Editor}
  Tests/EditMode
  Art/{Shaders,Materials}
  Data/        GameConfig and other tuning assets
  Scenes/      generated, do not edit by hand
docs/PLAN.md   development plan
```

## License

Proprietary — all rights reserved. See [LICENSE](LICENSE).
