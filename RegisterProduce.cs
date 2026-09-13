using HarmonyLib;
using Motions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProduceExample
{
    public class RegisterProduce
    {
        [HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.LoadScene))]
        [HarmonyPrefix]
        [HarmonyPriority(999)]  // done to prevent any order conflicts.
        private static void LoadScene(SCENE_STATE state, DelegateEvent onLoadScene)
        {
            if (state == SCENE_STATE.Battle)
            {
                BattleProduceInjector.RegisterProduce<UniqueAddonTest>("testproduce"); // must be registered like this or it will be unable to find the bundle
            }
        }

    }
}
