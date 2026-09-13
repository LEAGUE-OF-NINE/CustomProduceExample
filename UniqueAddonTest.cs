using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Motions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace ProduceExample
{
    public class F9KeyScanner : MonoBehaviour
    {
        public F9KeyScanner(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12))
            {
                OnF12Pressed();
            }
        }

        private void OnF12Pressed()
        {
            BattleProduceInjector.InitializeProducePrefabs();
            BattleProduceInjector.PlayProduce("testproduce");
            Debug.Log("F12 key pressed, testing produce");
        }
    }
    //
    // the class that needs to be passed into registration of the produce; holds its unique behaviour (in this bundle's case, unit movement)
    //
    public class UniqueAddonTest : ProduceAddOn
    {
        public UniqueAddonTest(IntPtr ptr) : base(ptr) { }
        private ProduceViewDriver _driver;
        public override void Init(ProduceBase produceBase)
        {
            _produceBase = produceBase;
        }

        public override void InitViews(
            Il2CppSystem.Collections.Generic.List<BattleUnitView> views,
            bool includeDeadUnit)
        {
            if (views == null || views.Count == 0 || _produceBase == null) return;

            var root = _produceBase.transform;
            var playerPivot = root.Find("Pivots/PlayerPivot"); 
            var enemyPivot = root.Find("Pivots/EnemyPivot");

            var pairs = new System.Collections.Generic.List<ProduceViewDriver.Pair>();

            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (v == null) continue;

                Transform pivot = (i == 0) ? playerPivot : enemyPivot;
                if (pivot == null) continue;

                pairs.Add(new ProduceViewDriver.Pair
                {
                    view = v.transform,
                    pivot = pivot,
                    viewStartLocalPos = v.transform.localPosition,
                    viewStartLocalScale = v.transform.localScale,
                    pivotStartLocalPos = pivot.localPosition,
                    pivotStartLocalScale = pivot.localScale,
                });
               // v.Appearance.ChangeMotion(MOTION_DETAIL.S1); (technically works, but throws an error to reciever)
            }

            if (pairs.Count == 0) return;

            _driver = gameObject.AddComponent<ProduceViewDriver>();
            _driver.pairs = pairs;
        }

        public override void PlayProduceOverride(int i, bool autoEnd, DelegateEvent endcallback)
        {
            if (_produceBase == null || _produceBase._director == null) return;

            var director = _produceBase._director;

            director.stopped -= new Action<PlayableDirector>(OnDirectorStopped);
            director.stopped += new Action<PlayableDirector>(OnDirectorStopped);

            director.Play();
        }

        private void OnDirectorStopped(PlayableDirector dir)
        {
            if (_driver != null)
            {
                _driver.Restore();
                UnityEngine.Object.Destroy(_driver);
                _driver = null;
            }

            var manager = ProduceManager.Instance?.TryCast<BattleProduceManager>();
            if (manager != null)
            {
                manager.EndProduce(false, false);
                manager.isProducing = false;
            }

            if (BattleCamManager.Instance != null)
                BattleCamManager.Instance.ResetFocus(false);

            if (_produceBase != null && _produceBase.gameObject != null)
                UnityEngine.Object.Destroy(_produceBase.gameObject);
        }
    }
}
