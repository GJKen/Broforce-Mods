using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityModManagerNet;

namespace CustomMapMultiplayer
{
    // The only boundary for the optional Swap Bros 2.1.5 integration.
    internal static class SwapBrosCompatibility
    {
        private const string ModId = "Swap Bros Mod";
        private const string MainTypeName = "Swap_Bros_Mod.Main";
        private const string SettingsTypeName = "Swap_Bros_Mod.Settings";
        private const string SelectedHeroTypeMethodName = "GetSelectedBroHeroType";
        private const string SettingsFieldName = "settings";
        private const string AlwaysChosenFieldName = "alwaysChosen";
        private const string IgnoreForcedBrosFieldName = "ignoreForcedBros";

        private const BindingFlags StaticMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly HashSet<string> SelectionFailureStages =
            new HashSet<string>(StringComparer.Ordinal);
        private static bool _selectionActivationLogged;

        private sealed class Binding
        {
            internal Assembly Assembly;
            internal string AssemblySource;
            internal Type SettingsType;
            internal MethodInfo GetSelectedHeroType;
            internal FieldInfo Settings;
            internal FieldInfo AlwaysChosen;
            internal FieldInfo IgnoreForcedBros;
        }

        internal static bool TryGetAlwaysChosenHeroType(int playerNum, out HeroType heroType)
        {
            heroType = HeroType.None;
            if (playerNum < 0 || playerNum >= 4)
            {
                return false;
            }

            var modEntry = UnityModManager.FindMod(ModId);
            if (modEntry == null || !modEntry.Active)
            {
                return false;
            }

            Binding binding;
            if (!TryResolveBinding(modEntry, out binding))
            {
                return false;
            }

            object settings;
            if (!TryReadField(binding.Settings, null, "main.settings-read", out settings))
            {
                return false;
            }
            if (settings == null)
            {
                return Fail(
                    "main.settings-read",
                    "null",
                    null,
                    "Swap_Bros_Mod.Main.settings returned null.");
            }
            if (settings.GetType() != binding.SettingsType)
            {
                return Fail(
                    "settings-type",
                    settings.GetType().FullName,
                    null,
                    "Main.settings returned an unexpected runtime type.");
            }

            object alwaysChosenValue;
            if (!TryReadField(
                    binding.AlwaysChosen,
                    settings,
                    "settings.alwaysChosen-read",
                    out alwaysChosenValue))
            {
                return false;
            }
            if (!(alwaysChosenValue is bool))
            {
                return Fail(
                    "settings.alwaysChosen-read",
                    Convert.ToString(alwaysChosenValue),
                    null,
                    "Swap_Bros_Mod.Settings.alwaysChosen did not return Boolean.");
            }
            if (!(bool)alwaysChosenValue)
            {
                return false;
            }

            object ignoreForcedBrosValue;
            if (!TryReadField(
                    binding.IgnoreForcedBros,
                    settings,
                    "settings.ignoreForcedBros-read",
                    out ignoreForcedBrosValue))
            {
                return false;
            }
            if (!(ignoreForcedBrosValue is bool))
            {
                return Fail(
                    "settings.ignoreForcedBros-read",
                    Convert.ToString(ignoreForcedBrosValue),
                    null,
                    "Swap_Bros_Mod.Settings.ignoreForcedBros did not return Boolean.");
            }
            if (!(bool)ignoreForcedBrosValue && HasMapForcedBro())
            {
                return Fail(
                    "settings.ignoreForcedBros",
                    "false-forced-bro",
                    null,
                    "A map-forced bro takes precedence over the selected bro.");
            }

            object selectedValue;
            if (!TryInvoke(
                    binding.GetSelectedHeroType,
                    new object[] { playerNum },
                    "main.GetSelectedBroHeroType-invoke",
                    out selectedValue))
            {
                return false;
            }
            if (!(selectedValue is HeroType))
            {
                return Fail(
                    "main.GetSelectedBroHeroType-return-type",
                    selectedValue == null ? "null" : selectedValue.GetType().FullName,
                    null,
                    "GetSelectedBroHeroType returned a value other than HeroType.");
            }

            heroType = (HeroType)selectedValue;
            if (heroType == HeroType.None)
            {
                return Fail(
                    "main.GetSelectedBroHeroType-return-value",
                    heroType.ToString(),
                    null,
                    "GetSelectedBroHeroType returned HeroType.None.");
            }

            if (!_selectionActivationLogged)
            {
                _selectionActivationLogged = true;
                DiagnosticLog.Info(
                    "Swap Bros Always spawn as chosen bro compatibility is active; " +
                    "selected hero lookup uses Swap_Bros_Mod.Main.GetSelectedBroHeroType.");
            }
            return true;
        }

        internal static void LogCompatibilitySnapshot(string trigger)
        {
            try
            {
                var modEntry = UnityModManager.FindMod(ModId);
                if (modEntry == null)
                {
                    DiagnosticLog.Info(
                        "OPTIONAL_BRO_MOD trigger=" + Sanitize(trigger) +
                        "; id=Swap_Bros_Mod; installed=false; active=false.");
                    return;
                }

                if (!modEntry.Active)
                {
                    DiagnosticLog.Info(
                        "OPTIONAL_BRO_MOD trigger=" + Sanitize(trigger) +
                        "; id=Swap_Bros_Mod; installed=true; active=false.");
                    return;
                }

                Binding binding;
                if (!TryResolveBinding(modEntry, out binding))
                {
                    DiagnosticLog.Info(
                        "OPTIONAL_BRO_MOD trigger=" + Sanitize(trigger) +
                        "; id=Swap_Bros_Mod; installed=true; active=true; " +
                        "selection={owner=Swap_Bros_Mod.Main,selectedHeroType=false," +
                        "settings=false,alwaysChosen=false,ignoreForcedBros=false}; " +
                        "selected=unavailable.");
                    return;
                }

                var selected = new List<string>();
                for (var playerNum = 0; playerNum < 4; playerNum++)
                {
                    selected.Add(ReadSelectedHeroType(binding.GetSelectedHeroType, playerNum));
                }

                DiagnosticLog.Info(
                    "OPTIONAL_BRO_MOD trigger=" + Sanitize(trigger) +
                    "; id=Swap_Bros_Mod; installed=true; active=true" +
                    "; version=" + Sanitize(Convert.ToString(
                        modEntry.Info == null ? null : modEntry.Info.Version)) +
                    "; assemblySource=" + Sanitize(binding.AssemblySource) +
                    "; assemblyVersion=" + Sanitize(Convert.ToString(binding.Assembly.GetName().Version)) +
                    "; selection={owner=Swap_Bros_Mod.Main,selectedHeroType=true," +
                    "settings=true,alwaysChosen=true,ignoreForcedBros=true}" +
                    "; selected=" + FormatSelected(selected) + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Optional bro mod compatibility snapshot failed: " + exception);
            }
        }

        private static bool TryResolveBinding(UnityModManager.ModEntry modEntry, out Binding binding)
        {
            binding = null;
            try
            {
                string assemblySource;
                var assembly = FindAssembly(modEntry, out assemblySource);
                if (assembly == null)
                {
                    return Fail(
                        "assembly",
                        "null",
                        null,
                        "Swap Bros assembly is unavailable; source=" + assemblySource);
                }

                var mainType = assembly.GetType(MainTypeName, false);
                if (mainType == null)
                {
                    return Fail(
                        "main-type",
                        "null",
                        null,
                        "Swap_Bros_Mod.Main is unavailable; assembly=" + assembly.FullName);
                }

                var settingsType = assembly.GetType(SettingsTypeName, false);
                if (settingsType == null)
                {
                    return Fail(
                        "settings-type",
                        "null",
                        null,
                        "Swap_Bros_Mod.Settings is unavailable; assembly=" + assembly.FullName);
                }

                var settingsField = mainType.GetField(SettingsFieldName, StaticMembers);
                if (!IsField(settingsField, mainType, settingsType, true))
                {
                    return Fail(
                        "main.settings-field",
                        settingsField == null ? "null" : settingsField.ToString(),
                        null,
                        "Swap_Bros_Mod.Main.settings must be a static Swap_Bros_Mod.Settings field.");
                }

                var selectedMethod = FindStaticMethod(
                    mainType,
                    SelectedHeroTypeMethodName,
                    new[] { typeof(int) },
                    typeof(HeroType));
                if (selectedMethod == null)
                {
                    return Fail(
                        "main.GetSelectedBroHeroType-signature",
                        "null",
                        null,
                        "Swap_Bros_Mod.Main.GetSelectedBroHeroType(Int32) with HeroType return type is unavailable.");
                }

                var alwaysChosenField = settingsType.GetField(AlwaysChosenFieldName, InstanceMembers);
                if (!IsField(alwaysChosenField, settingsType, typeof(bool), false))
                {
                    return Fail(
                        "settings.alwaysChosen-field",
                        alwaysChosenField == null ? "null" : alwaysChosenField.ToString(),
                        null,
                        "Swap_Bros_Mod.Settings.alwaysChosen must be an instance Boolean field.");
                }

                var ignoreForcedBrosField = settingsType.GetField(IgnoreForcedBrosFieldName, InstanceMembers);
                if (!IsField(ignoreForcedBrosField, settingsType, typeof(bool), false))
                {
                    return Fail(
                        "settings.ignoreForcedBros-field",
                        ignoreForcedBrosField == null ? "null" : ignoreForcedBrosField.ToString(),
                        null,
                        "Swap_Bros_Mod.Settings.ignoreForcedBros must be an instance Boolean field.");
                }

                binding = new Binding
                {
                    Assembly = assembly,
                    AssemblySource = assemblySource,
                    SettingsType = settingsType,
                    GetSelectedHeroType = selectedMethod,
                    Settings = settingsField,
                    AlwaysChosen = alwaysChosenField,
                    IgnoreForcedBros = ignoreForcedBrosField
                };
                return true;
            }
            catch (Exception exception)
            {
                return Fail(
                    "resolve",
                    "exception",
                    exception,
                    "Resolving Swap Bros runtime declarations failed.");
            }
        }

        private static Assembly FindAssembly(UnityModManager.ModEntry modEntry, out string source)
        {
            var preferredAssembly = modEntry == null ? null : modEntry.Assembly;
            if (ContainsMainType(preferredAssembly))
            {
                source = "mod-entry";
                return preferredAssembly;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (ContainsMainType(assembly))
                {
                    source = "app-domain";
                    return assembly;
                }
            }

            source = preferredAssembly == null ? "unavailable" : "mod-entry-without-main-type";
            return null;
        }

        private static bool ContainsMainType(Assembly assembly)
        {
            if (assembly == null)
            {
                return false;
            }

            try
            {
                return assembly.GetType(MainTypeName, false) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsField(FieldInfo field, Type owner, Type fieldType, bool isStatic)
        {
            return field != null && field.DeclaringType == owner &&
                field.IsStatic == isStatic && field.FieldType == fieldType;
        }

        private static MethodInfo FindStaticMethod(Type type, string name, Type[] parameters, Type returnType)
        {
            if (type == null)
            {
                return null;
            }

            var method = type.GetMethod(name, StaticMembers, null, parameters, null);
            return method == null || method.DeclaringType != type ||
                !method.IsStatic || method.ReturnType != returnType ? null : method;
        }

        private static bool TryReadField(FieldInfo field, object instance, string stage, out object value)
        {
            try
            {
                value = field.GetValue(instance);
                return true;
            }
            catch (Exception exception)
            {
                value = null;
                return Fail(stage, "exception", exception, "Reading Swap Bros field failed.");
            }
        }

        private static bool TryInvoke(MethodInfo method, object[] arguments, string stage, out object value)
        {
            try
            {
                value = method.Invoke(null, arguments);
                return true;
            }
            catch (Exception exception)
            {
                value = null;
                return Fail(
                    stage,
                    "exception",
                    UnwrapInvocationException(exception),
                    "Invoking Swap Bros method failed.");
            }
        }

        private static bool HasMapForcedBro()
        {
            return Map.MapData != null &&
                (Map.MapData.forcedBro != HeroType.Random ||
                 (Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count > 0));
        }

        private static bool Fail(string stage, string returnValue, Exception exception, string detail)
        {
            if (SelectionFailureStages.Add(stage))
            {
                DiagnosticLog.Warning(
                    "SWAP_BROS_SELECTION_LOOKUP_FAILURE stage=" + Sanitize(stage) +
                    "; exception=" + Sanitize(exception == null ? "none" : exception.ToString()) +
                    "; return=" + Sanitize(returnValue) +
                    "; detail=" + Sanitize(detail) +
                    "; nativeSpawnActive=true.");
            }
            return false;
        }

        private static Exception UnwrapInvocationException(Exception exception)
        {
            var invocationException = exception as TargetInvocationException;
            return invocationException == null || invocationException.InnerException == null
                ? exception
                : invocationException.InnerException;
        }

        private static string ReadSelectedHeroType(MethodInfo method, int playerNum)
        {
            try
            {
                var value = method.Invoke(null, new object[] { playerNum });
                return value == null ? string.Empty : Convert.ToString(value);
            }
            catch (Exception exception)
            {
                return "<error:" + UnwrapInvocationException(exception).GetType().Name + ">";
            }
        }

        private static string FormatSelected(IList<string> selected)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < selected.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }
                builder.Append("P");
                builder.Append(index + 1);
                builder.Append("=");
                builder.Append(Sanitize(selected[index]));
            }
            return builder.ToString();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unknown";
            }

            var builder = new StringBuilder();
            var length = System.Math.Min(value.Length, 120);
            for (var index = 0; index < length; index++)
            {
                var current = value[index];
                builder.Append(current == '\r' || current == '\n' || current == ';' || current == ','
                    ? '_'
                    : char.IsControl(current) ? '?' : current);
            }
            return builder.Length == 0 ? "unknown" : builder.ToString();
        }
    }
}
