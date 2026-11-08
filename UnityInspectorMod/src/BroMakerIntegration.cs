using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class BroMakerIntegration
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static object ListBros()
        {
            object result = null;
            RunOnMainThread(() =>
            {
                Type storageType = FindType("BroMakerLib.Storages.BroMakerStorage");
                PropertyInfo brosProperty = storageType?.GetProperty("Bros", AnyStatic);
                if (brosProperty == null)
                    throw new InvalidOperationException("BroMaker storage is not available");

                var bros = new List<object>();
                var storedBros = brosProperty.GetValue(null, null) as IEnumerable;
                if (storedBros == null)
                    throw new InvalidOperationException("BroMaker character list is unavailable");

                foreach (object storedHero in storedBros)
                {
                    if (storedHero == null) continue;
                    string name = GetStringMember(storedHero, "name");
                    if (string.IsNullOrEmpty(name)) continue;
                    object mod = GetMember(storedHero, "mod");
                    bros.Add(new
                    {
                        name,
                        modVersion = GetStringMember(mod, "Version"),
                        networkAvailable = GetUnavailableReason(storedHero) == null,
                        unavailableReason = GetUnavailableReason(storedHero)
                    });
                }

                result = new { success = true, bros = bros.ToArray() };
            });
            return result;
        }

        public static object SwitchBro(string broName, int playerNum)
        {
            if (string.IsNullOrEmpty(broName))
                return new { success = false, error = "Bro name is required" };
            if (playerNum < 0 || playerNum > 3)
                return new { success = false, error = "Player number must be 0-3" };

            object result = null;
            RunOnMainThread(() =>
            {
                if (HeroController.players == null || HeroController.players.Length <= playerNum ||
                    HeroController.players[playerNum] == null || HeroController.players[playerNum].character == null)
                    throw new InvalidOperationException("The selected player is not active");
                if (!HeroController.players[playerNum].IsMine)
                    throw new InvalidOperationException("The selected player is owned by the remote peer");

                Type storageType = FindType("BroMakerLib.Storages.BroMakerStorage");
                Type storedHeroType = FindType("BroMakerLib.Storages.StoredHero");
                Type loadHeroType = FindType("BroMakerLib.Loaders.LoadHero");
                if (storageType == null || storedHeroType == null || loadHeroType == null)
                    throw new InvalidOperationException("BroMaker runtime API is unavailable");

                MethodInfo getHero = storageType.GetMethod("GetStoredHeroByName", PublicStatic);
                if (getHero == null)
                    throw new InvalidOperationException("BroMaker character lookup API is unavailable");

                object[] lookupArgs = { broName, null };
                if (!(bool)getHero.Invoke(null, lookupArgs) || lookupArgs[1] == null)
                    throw new ArgumentException("BroMaker character was not found: " + broName);

                object storedHero = lookupArgs[1];
                string unavailableReason = GetUnavailableReason(storedHero);
                if (unavailableReason != null)
                    throw new InvalidOperationException("BroMaker character is unavailable online: " + unavailableReason);

                FieldInfo previousSpawnInfo = loadHeroType.GetField("previousSpawnInfo", PublicStatic);
                FieldInfo wasFirstDeployment = loadHeroType.GetField("wasFirstDeployment", PublicStatic);
                if (previousSpawnInfo == null || wasFirstDeployment == null)
                    throw new InvalidOperationException("BroMaker spawn state API is unavailable");

                Array spawnInfo = previousSpawnInfo.GetValue(null) as Array;
                Array firstDeployment = wasFirstDeployment.GetValue(null) as Array;
                if (spawnInfo == null || firstDeployment == null || spawnInfo.Length <= playerNum || firstDeployment.Length <= playerNum)
                    throw new InvalidOperationException("BroMaker spawn state is unavailable");

                spawnInfo.SetValue(Enum.Parse(typeof(Player.SpawnType), "TriggerSwapBro"), playerNum);
                firstDeployment.SetValue(false, playerNum);

                MethodInfo loadBro = storedHeroType.GetMethod("LoadBro", new[] { typeof(int) });
                if (loadBro == null)
                    throw new InvalidOperationException("BroMaker LoadBro entry is unavailable");

                object loaded = loadBro.Invoke(storedHero, new object[] { playerNum });
                Component component = loaded as Component;
                if (component == null)
                {
                    result = new { success = false, error = "BroMaker LoadBro returned no character", broName, playerNum };
                    return;
                }

                result = new
                {
                    success = true,
                    requestedBro = broName,
                    playerNum,
                    characterType = loaded.GetType().FullName,
                    gameObject = component.gameObject != null ? component.gameObject.name : null,
                    position = new { x = component.transform.position.x, y = component.transform.position.y, z = component.transform.position.z },
                    networkPath = "BroMaker StoredHero.LoadBro -> LoadHero.WithCustomBroInfo -> InstantiateBuffered"
                };
            });
            return result;
        }

        private static string GetUnavailableReason(object storedHero)
        {
            Type networkType = FindType("BroMakerLib.BroMakerNetwork");
            MethodInfo method = networkType?.GetMethod("GetBroUnavailableReason", AnyStatic, null,
                new[] { storedHero.GetType() }, null);
            if (method == null)
                return "BroMaker network availability API is unavailable";
            return method.Invoke(null, new[] { storedHero }) as string;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static object GetMember(object target, string name)
        {
            if (target == null) return null;
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(target);
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(target, null);
        }

        private static string GetStringMember(object target, string name)
        {
            return GetMember(target, name) as string;
        }

        private static void RunOnMainThread(Action action)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == 1)
            {
                action();
                return;
            }
            MainThreadDispatcher.EnqueueAndWait(action, 5000);
        }
    }
}
