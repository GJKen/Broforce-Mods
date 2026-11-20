using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    // The native pool owns lifetime; reset the tag on acquisition, without a scene scan.
    internal sealed class WorldCarrier : MonoBehaviour
    {
        internal string Prefab, Type;
        internal BroforceObject Actor;
        internal bool Presentation;
        internal bool Restored;
        internal int Owner, Serial;
    }

    internal static partial class HarmonyDiagnostics
    {
        private static void PatchWorldCarriers()
        {
            foreach (var method in typeof(EffectsController).GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == "InstantiateEffect" && !method.IsGenericMethod &&
                    method.GetParameters().Length == 3 && method.GetParameters()[1].ParameterType == typeof(Vector3))
                    PatchWorldMethod(method, null, "WorldCarrierInstantiatePostfix");
            PatchWorldMethod(AccessTools.Method(typeof(FlashBangExplosion), "Setup", null, null), null, "WorldCarrierSetupPostfix");
            foreach (var type in typeof(FlashBangExplosion).Assembly.GetTypes())
                if (typeof(FlashBangExplosion).IsAssignableFrom(type))
                    foreach (var method in type.GetMethods(WorldFields | BindingFlags.DeclaredOnly))
                        if (method.Name == "Update" || method.Name == "EffectFirstUpdate" || method.Name == "CreateFlashBangPoint" ||
                            method.Name == "TryAssassinateUnits" || method.Name == "RunPoints")
                            PatchWorldMethod(method, "WorldCarrierContextPrefix", null, null, "WorldProjectileContextFinalizer");
            PatchWorldLasers();
        }

        private static void WorldCarrierInstantiatePostfix(object __0, object __result)
        {
            var effect = __result as FlashBangExplosion;
            var prefab = __0 as UnityEngine.Object;
            if (effect == null || prefab == null) return;
            var tag = effect.GetComponent<WorldCarrier>() ?? effect.gameObject.AddComponent<WorldCarrier>();
            tag.Prefab = prefab.name; tag.Type = prefab.GetType().FullName;
            tag.Actor = null; tag.Presentation = _worldApplying != 0;
            tag.Serial = 0; tag.Restored = false;
        }

        private static void WorldCarrierSetupPostfix(FlashBangExplosion __instance, int num, MonoBehaviour FiredBy, DirectionEnum direction)
        {
            if (_worldEpoch == 0 || !IsWorldSession()) return;
            var tag = __instance.GetComponent<WorldCarrier>();
            if (tag == null) return;
            tag.Actor = FiredBy as BroforceObject;
            var source = FiredBy as NetworkObject;
            tag.Presentation = _worldApplying != 0 || source != null && !source.IsMine;
            // Persistent descriptors also own carriers created inside replayed explosion effects.
            if (tag.Presentation && !_worldRestoringEffect) { __instance.EffectDie(); return; }
            if (tag.Presentation) return;
            tag.Owner = PID.MyID.AsInt; tag.Serial = ++_worldEffectSerial;
            TrackWorldPersistentEffect(__instance, tag, NID.NoID);
        }

        private static bool WorldCarrierContextPrefix(FlashBangExplosion __instance, MethodBase __originalMethod, out WorldDamageContext __state)
        {
            var tag = __instance.GetComponent<WorldCarrier>();
            __state = new WorldDamageContext { Previous = _worldDamageSource };
            if (tag == null || _worldEpoch == 0 || !IsWorldSession()) return true;
            if (tag.Restored && __originalMethod.Name == "EffectFirstUpdate") return false;
            // Descriptors advance replica grids. Running the native frontier again repeats cell effects.
            if (tag.Presentation && (__originalMethod.Name == "Update" || __originalMethod.Name == "RunPoints")) return false;
            __state.PreviousStatus = _worldStatusSource; __state.StatusContext = true;
            _worldStatusSource = tag.Presentation ? null : tag.Actor as Unit;
            if (tag.Presentation) { __state.VisualReplay = true; _worldApplying++; _worldDamageSource = null; }
            else _worldDamageSource = tag.Actor;
            return true;
        }

    }
}
