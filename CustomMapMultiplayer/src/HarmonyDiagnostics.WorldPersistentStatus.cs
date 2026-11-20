using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly FieldInfo WorldStun = AccessTools.Field(typeof(TestVanDammeAnim), "stunTime");
        private static readonly FieldInfo WorldFrozen = AccessTools.Field(typeof(TestVanDammeAnim), "frozenTime");
        private static readonly FieldInfo WorldAcidTime = AccessTools.Field(typeof(TestVanDammeAnim), "acidMeltTimer");
        private static readonly FieldInfo WorldInseminated = AccessTools.Field(typeof(TestVanDammeAnim), "inseminatedCounter");
        private static readonly FieldInfo WorldInsemination = AccessTools.Field(typeof(TestVanDammeAnim), "inseminationCounter");
        private static readonly FieldInfo WorldInseminationFrame = AccessTools.Field(typeof(TestVanDammeAnim), "inseminationFrame");
        private static readonly FieldInfo WorldDefaultMaterial = AccessTools.Field(typeof(TestVanDammeAnim), "defaultMaterial");
        private static readonly FieldInfo WorldTearGas = AccessTools.Field(typeof(Mook), "tearGasChoking");

        private static void PatchWorldPersistentStatus()
        {
            // These native routines settle timers/parasite spawning. Replicas receive their results.
            foreach (var name in new[] { "RunCoveredInAcid", "BurstChest", "CheckPlasmaDeath", "DisConnectFaceHugger" })
                PatchWorldMethod(AccessTools.Method(typeof(TestVanDammeAnim), name, null, null), "WorldPersistentStatusLogicPrefix", null);
        }

        private static bool WorldPersistentStatusLogicPrefix(TestVanDammeAnim __instance)
        { return !IsWorldReplica(__instance); }

        private static float WorldStatusAge(WorldSnapshot s)
        {
            return Mathf.Max(s.StatusReceivedAt > 0f ? Time.time - s.StatusReceivedAt : 0f,
                Mathf.Max(0f, PingController.GetLocalTime(s.Owner == PID.TargetServer.AsInt ? PID.ServerID : new PID(s.Owner)) - s.StatusTime) * Time.timeScale);
        }

        private static void CaptureWorldPersistentStatus(TestVanDammeAnim u, WorldSnapshot s)
        {
            s.StatusTime = Time.realtimeSinceStartup;
            s.StunTime = Mathf.Max(0f, (float)WorldStun.GetValue(u));
            s.FrozenTime = Mathf.Max(0f, (float)WorldFrozen.GetValue(u));
            s.PlasmaTime = Mathf.Max(0f, (float)WorldPlasmaCounter.GetValue(u));
            s.PlasmaDamage = (int)WorldPlasmaDamage.GetValue(u);
            s.ShockTime = Mathf.Max(0f, (float)WorldShockCounter.GetValue(u));
            s.Acid = u.hasBeenCoverInAcid; s.AcidTime = (float)WorldAcidTime.GetValue(u);
            s.TearGas = u is Mook && (bool)WorldTearGas.GetValue(u);
            s.Inseminator = Registry.GetNID(u.inseminatorUnit);
            s.InseminatedTime = Mathf.Max(0f, (float)WorldInseminated.GetValue(u));
            s.InseminationTime = (float)WorldInsemination.GetValue(u);
            s.InseminationFrame = (int)WorldInseminationFrame.GetValue(u);
            var renderer = u.GetComponent<Renderer>();
            if (s.Acid && renderer != null && renderer.sharedMaterial != null)
                s.StatusMaterial = renderer.sharedMaterial.name.Replace(" (Instance)", "");
        }

        private static bool WorldPersistentStatusChanged(WorldSnapshot a, WorldSnapshot b)
        {
            return (a.StunTime > 0f) != (b.StunTime > 0f) || (a.FrozenTime > 0f) != (b.FrozenTime > 0f) ||
                a.PlasmaDamage != b.PlasmaDamage || (a.ShockTime > 0f) != (b.ShockTime > 0f) ||
                a.Acid != b.Acid || a.TearGas != b.TearGas || a.Inseminator != b.Inseminator ||
                (a.InseminatedTime > 0f) != (b.InseminatedTime > 0f) || a.StatusMaterial != b.StatusMaterial;
        }

        private static void WriteWorldPersistentStatus(BinaryWriter w, WorldSnapshot s)
        {
            w.Write(s.StatusTime); w.Write(s.StunTime); w.Write(s.FrozenTime); w.Write(s.PlasmaTime); w.Write(s.PlasmaDamage);
            w.Write(s.ShockTime); w.Write(s.Acid); w.Write(s.AcidTime); w.Write(s.TearGas);
            WriteWorldId(w, s.Inseminator); w.Write(s.InseminatedTime); w.Write(s.InseminationTime);
            w.Write(s.InseminationFrame); w.Write(s.StatusMaterial);
        }

        private static void ReadWorldPersistentStatus(BinaryReader r, WorldSnapshot s)
        {
            s.StatusTime = r.ReadSingle(); s.StunTime = r.ReadSingle(); s.FrozenTime = r.ReadSingle();
            s.PlasmaTime = r.ReadSingle(); s.PlasmaDamage = r.ReadInt32(); s.ShockTime = r.ReadSingle();
            s.Acid = r.ReadBoolean(); s.AcidTime = r.ReadSingle(); s.TearGas = r.ReadBoolean();
            s.Inseminator = ReadWorldId(r); s.InseminatedTime = r.ReadSingle(); s.InseminationTime = r.ReadSingle();
            s.InseminationFrame = r.ReadInt32(); s.StatusMaterial = r.ReadString();
        }

        private static bool ResolveWorldInseminator(WorldSnapshot s)
        {
            return s.Inseminator == NID.NoID || Registry.GetObject(s.Inseminator) is AlienFaceHugger ||
                WorldRemovedObjects.Contains(s.Inseminator) || WorldRemovedObjects.Contains(s.Inseminator.BaseID);
        }

        private static void ApplyWorldPersistentStatus(TestVanDammeAnim u, WorldSnapshot s, float age)
        {
            WorldStun.SetValue(u, Mathf.Max(0f, s.StunTime - age));
            var frozen = Mathf.Max(0f, s.FrozenTime - age);
            var wasFrozen = (float)WorldFrozen.GetValue(u) > 0f;
            WorldFrozen.SetValue(u, frozen);
            if (frozen > 0f && !wasFrozen) SetWorldFrozenMaterial(u);
            else if (frozen == 0f && wasFrozen) u.UnFreeze();
            WorldPlasmaCounter.SetValue(u, Mathf.Max(0f, s.PlasmaTime - age));
            WorldPlasmaDamage.SetValue(u, s.PlasmaTime > age ? s.PlasmaDamage : 0);
            WorldShockCounter.SetValue(u, Mathf.Max(0f, s.ShockTime - age));
            if (u is Mook) WorldTearGas.SetValue(u, s.TearGas && s.StunTime > age);
            var wasAcid = u.hasBeenCoverInAcid;
            u.hasBeenCoverInAcid = s.Acid;
            WorldAcidTime.SetValue(u, s.AcidTime - age);
            if (s.Acid && (!wasAcid || age == 0f))
            {
                foreach (Material material in Resources.FindObjectsOfTypeAll(typeof(Material)))
                    if (material.name == s.StatusMaterial)
                    { u.GetComponent<Renderer>().sharedMaterial = material; ((SpriteSM)WorldUnitSprite.GetValue(u)).RecalcTexture(); break; }
                var gun = (SpriteSM)WorldUnitGunSprite.GetValue(u);
                if (gun != null) gun.GetComponent<Renderer>().enabled = false;
            }
            else if (wasAcid && !s.Acid)
            {
                u.GetComponent<Renderer>().sharedMaterial = (Material)WorldDefaultMaterial.GetValue(u);
                var gun = (SpriteSM)WorldUnitGunSprite.GetValue(u);
                if (gun != null) gun.GetComponent<Renderer>().enabled = true;
            }
            var parasite = Registry.GetObject(s.Inseminator) as AlienFaceHugger;
            if (u.inseminatorUnit != null && u.inseminatorUnit != parasite) u.inseminatorUnit.inseminatedUnit = null;
            u.inseminatorUnit = parasite;
            if (parasite != null) { parasite.inseminatedUnit = u; parasite.SetFaceHuggingFrame(s.InseminationFrame); }
            WorldInseminated.SetValue(u, Mathf.Max(0f, s.InseminatedTime - age));
            WorldInsemination.SetValue(u, s.InseminationTime + age);
            WorldInseminationFrame.SetValue(u, s.InseminationFrame);
            // Never restore inseminatedByLocalUnit: only the native owner may spawn a chestburster.
        }

        private static void SetWorldFrozenMaterial(TestVanDammeAnim u)
        {
            var sprite = (SpriteSM)WorldUnitSprite.GetValue(u);
            var gun = (SpriteSM)WorldUnitGunSprite.GetValue(u);
            var targets = new[] { sprite, gun };
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null || target.meshRender == null) continue;
                var field = AccessTools.Field(typeof(TestVanDammeAnim), i == 0 ? "_frozenMaterial" : "_frozenGunMaterial");
                var material = (Material)field.GetValue(u);
                if (material == null)
                {
                    material = new Material(Shader.Find("Unlit/Depth Cutout With Image - Frozen"));
                    material.mainTexture = target.meshRender.sharedMaterial.mainTexture;
                    material.SetVector("_SpriteSize", target.pixelDimensions);
                    material.SetColor("_FrozenCol", new Color(.094f, .388f, .718f, 1f));
                    field.SetValue(u, material);
                }
                target.meshRender.material = material; target.SetUV2();
            }
        }
    }
}
