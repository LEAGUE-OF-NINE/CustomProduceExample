# CustomProduceExample

# Overview
The game has a built-in "produce" system — short scripted sequences that play during battle (unit animations, camera moves, etc). This mod lets you:
- Register a custom produce from an AssetBundle.
- Drive it through the game's own BattleProduceManager.
- Attach a custom ProduceAddOn subclass to control what the produce actually does.
- Play custom timelines that move real battle units around.

The game's native produce code handles everything else (camera, flow, cleanup). We just supply the prefab, the addon, and a small amount of glue.

# How the native produce lifecycle works
Roughly, when a produce is played:
```
PlayProduce(key)
    ->
manager._curProduceBase = produceBase
manager.isProducing      = true
manager.isSetProduce     = true     <-- critical, see below
    ->
produceBase.Init(manager)
produceBase.InitViews(views, false)
manager.SetProduce()
manager.PlayProduce(index, autoEnd, endCallback, isMainCamera: true)
    ->
produceBase.PlayProduce_Before()
produceBase.PlayProduceOverride()
    -> dispatches to every ProduceAddOn in _produceAddOn list
        -> addon.PlayProduceOverride()
```

PlayProduce internally does director.Stop(), director.time = startTime, then director.Play(). It then dispatches to every addon so each can do its own thing.

When the timeline finishes, director.stopped fires. That's the hook for cleanup.

# Native per-frame clamp (why we need runtime patches)
While a produce plays, the native BattleProduceManager.Update() runs a per-frame check. When the timeline reaches roughly duration - 2 frames, it does:

```
director.Pause();
director.time = <clamped value>;
```
...on every frame from then on. This is meant to hand off to a native end-handler. Since our synthetic produce has no such handler, the director gets frozen at that time forever, director.stopped never fires, and the produce never ends.

The runtime patches suppress both calls for directors whose GameObject name starts with CustomBattleProduce_:

PlayableDirector.Pause() returns false (skipped).

PlayableDirector.set_time(value) returns false when the value looks like the clamp (near the observed pin value, or a small backwards step).

With these suppressed, time keeps climbing, hits real duration, and director.stopped fires normally.

# File structure
File -> Purpose

Main.cs: BepInEx plugin entry point. Registers types, applies Harmony patches.

ProduceRuntimePatches.cs: Harmony patches that suppress the native clamp on our produces.

RegisterProduce.cs: Harmony patch on GlobalGameManager.LoadScene that registers produces when the battle scene loads.

UniqueAddonTest.cs: Your ProduceAddOn subclass. Controls what the produce does.

ProduceViewDriver.cs: Drives battle unit transforms from pivot transforms each frame

# Registering a produce
From your LoadScene patch (or anywhere after the bundle is loaded):

```
BattleProduceInjector.RegisterProduce<MyAddon>("my_produce");
```
Under the hood:

The addon factory is stored in a dictionary keyed by produce name.

MotionData.produceAssets[key] is checked for the AssetBundle.

The bundle is scanned for the first GameObject asset, which is instantiated and stored as the template.

The template is instantiated at registration and kept deactivated. Each PlayProduce call instantiates a fresh copy of it.

Overloads:
```
RegisterProduce(string key)
    // uses MotionData.produceAssets[key] as the bundle, no custom addon

RegisterProduce(string key, AssetBundle bundle)
    // explicit bundle, no custom addon

RegisterProduce<TAddon>(string key)
    // uses MotionData.produceAssets[key], custom addon

RegisterProduce<TAddon>(string key, AssetBundle bundle)
    // explicit bundle and addon
```

# Authoring a produce prefab
Required structure (in our example bundle's case):

```
TestProduce                 <- root
    ├── PlayableDirector    <- with a TimelineAsset assigned
    └── Pivots
            ├── PlayerPivot
            └── EnemyPivot
```
Notes:

The PlayableDirector must be on the root or reachable via produceGO.GetComponent<PlayableDirector>().

The TimelineAsset must be assigned to the director's playableAsset before packing into the bundle.

Pivots/PlayerPivot and Pivots/EnemyPivot are the convention this system uses. If you want different names, update the lookup in your addon's InitViews.

Animation tracks in the timeline should be bound to PlayerPivot and EnemyPivot (not to the actual units).

# What gets animated
The timeline animates the pivots. The pivots are empty GameObjects. Your addon reads their transforms each frame and applies the delta to the real BattleUnitView transforms.

This is what ProduceViewDriver does. It captures the view and pivot starting local positions and scales at InitViews, then every LateUpdate writes:

```
view.localPosition = viewStartLocalPosition + (pivot.localPosition - pivotStartLocalPosition)
view.localScale    = viewStartLocalScale * (pivot.localScale / pivotStartLocalScale)
```
Late update ordering matters because the battle system writes the view's position in its own Update.

# Writing an addon
Subclass ProduceAddOn. Override what you need:

```
public class MyAddon : ProduceAddOn
{
    public MyAddon(IntPtr ptr) : base(ptr) { }

    public override void Init(ProduceBase produceBase) { }

    public override void InitViews(
        Il2CppSystem.Collections.Generic.List<BattleUnitView> views,
        bool includeDeadUnit) { }

    public override void PlayProduceOverride(
        int i, bool autoEnd, DelegateEvent endcallback) { }

    private void OnDirectorStopped(PlayableDirector dir) { }
}
```
Init:
Called once before InitViews. Store produceBase for later.

InitViews:
Called with the list of live battle units. This is where you:

- Find the pivot transforms under produceBase.transform.

- Build the view-pivot pairs.

- Attach a ProduceViewDriver to the produce GameObject.

Do not call base.InitViews(...). The base implementation dispatches back to every addon, including yours, causing infinite recursion.

PlayProduceOverride:
Called by the native pipeline to start the produce. Subscribe to director.stopped here, then call director.Play().

The base already calls Play() internally. If you just want standard behaviour, don't override this — but if you don't override it, you also don't get a chance to hook director.stopped, so overriding is normal.

Do not call base.PlayProduceOverride(...). Same recursion risk as InitViews.

OnDirectorStopped:
Fires when the timeline ends. Clean up here:

Restore any transforms you modified.

Destroy any components you added.

Call manager.EndProduce(false, false), set manager.isProducing = false, call BattleCamManager.Instance.ResetFocus(false), destroy the produce GameObject, do not override SetProduceEnd.

ProduceAddOn.SetProduceEnd is a lifecycle callback that fires from inside native cleanup. Calling manager.EndProduce() from inside it re-enters native cleanup, which re-enters SetProduceEnd, which causes a stack overflow. Let the native system call it if it needs to; do your cleanup from director.stopped instead.

Testing
Attach F12Scanner to a persistent GameObject at plugin load (its in the example):

```
var go = new GameObject("F12Scanner");
go.AddComponent<F12Scanner>();
UnityEngine.Object.DontDestroyOnLoad(go);
```
Press F12 in battle to trigger a test produce. F12Scanner calls:

```
BattleProduceInjector.InitializeProducePrefabs();
BattleProduceInjector.PlayProduce("testproduce");
```

This is also how you should implement playing a Produce when you have a real condition. (I.e turn end cutscene or whatever)

# Addon must be registered as an IL2CPP type
```
ClassInjector.RegisterTypeInIl2Cpp<MyAddon>();
```
Do this in Main.Load(). Without it, AddComponent<MyAddon>() fails.

Addon must not be AddComponent-ed before the type is registered

The factory registered in RegisterProduce<TAddon> runs at PlayProduce time, which is fine. But if you're calling AddComponent<TAddon> manually anywhere, do it after the ClassInjector registration has completed.


# Runtime patches reference
All patches live in ProduceRuntimePatches.cs.

PlayableDirector.Pause (prefix, returns false)
Suppresses native per-frame pause on our produce directors. Only applies when the director's GameObject name starts with CustomBattleProduce_.

PlayableDirector.set_time (prefix, conditional)
Suppresses the native pin write. Matches either:

Value within tolerance of the observed pin time 0.867333....

Any backwards write while the director is playing, under 50ms.

Both conditions only apply to our produce directors.

ProduceBase.SetLookRotateObject (prefix, returns false)
Skipped entirely for all produces. The base method iterates a List<GameObject> that is null on synthetic produces and would NRE. Harmless to skip; the native produce addon populates this list and we don't use that addon.

# Debugging checklist
If a produce silently does nothing:

Check manager.isSetProduce is true before SetProduce().

Check the addon factory is registered (produceAddOns.ContainsKey(key)).

Check produceBase._produceAddOn contains your addon after factory runs.

Check the director's playableAsset is a valid TimelineAsset.

Check director.stopped is subscribed to.

If the timeline plays but units don't move:

Check InitViews found the pivots (log root.Find("Pivots/PlayerPivot")).

Check the driver was attached (AddComponent<ProduceViewDriver>()).

Check the driver's LateUpdate is running (attach a log line once).

Check the timeline tracks are bound to the pivots (use director.GetGenericBinding(track)).

If cleanup doesn't run:

Check director.stopped is firing (log in the handler).

Check SuppressDirectorPause is active so the timeline actually reaches the end.
