using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityModManagerNet;

namespace BroforceCustomMapMultiplayer
{
    // Read-only weak integration for optional bro-selection mods.
    internal static class OptionalBroModDiagnostics
    {
        private const string SwapBrosModId = "Swap Bros Mod";
        private const string SwapBrosApiType = "Swap_Bros_Mod.API";

        internal static void LogCompatibilitySnapshot(string trigger)
        {
            try
            {
                var modEntry = UnityModManager.FindMod(SwapBrosModId);
                if (modEntry == null)
                {
                    DiagnosticLog.Info(
                        "OPTIONAL_BRO_MOD trigger=" + Sanitize(trigger) +
                        "; id=Swap_Bros_Mod; installed=false; active=false.");
                    return;
                }

                var assembly = modEntry.Assembly;
                var apiType = assembly == null ? null : assembly.GetType(SwapBrosApiType);
                var getAvailableBros = FindPublicStaticMethod(apiType, "GetAvailableBros", Type.EmptyTypes);
                var getSelectedBroName = FindPublicStaticMethod(apiType, "GetSelectedBroName", new[] { typeof(int) });
                var getSelectedBroHeroType = FindPublicStaticMethod(
                    apiType,
                    "GetSelectedBroHeroType",
                    new[] { typeof(int) });
                var capabilities =
                    "availableBros=" + (getAvailableBros != null) +
                    ",selectedName=" + (getSelectedBroName != null) +
                    ",selectedHeroType=" + (getSelectedBroHeroType != null);
                var version = modEntry.Info == null
                    ? "unknown"
                    : Convert.ToString(modEntry.Info.Version);
                var assemblyVersion = assembly == null
                    ? "unknown"
                    : Convert.ToString(assembly.GetName().Version);
                var moduleVersionId = assembly == null
                    ? "unknown"
                    : assembly.ManifestModule.ModuleVersionId.ToString("N");

                if (!modEntry.Active || getAvailableBros == null)
                {
                    DiagnosticLog.Info(
                        "OPTIONAL_BRO_MOD trigger=" + Sanitize(trigger) +
                        "; id=Swap_Bros_Mod; installed=true; active=" + modEntry.Active +
                        "; version=" + Sanitize(version) +
                        "; assemblyVersion=" + Sanitize(assemblyVersion) +
                        "; moduleVersionId=" + Sanitize(moduleVersionId) +
                        "; api={" + capabilities + "}; roster=unavailable.");
                    return;
                }

                var roster = ReadStringList(getAvailableBros.Invoke(null, null));
                var selected = new List<string>();
                for (var playerNum = 0; playerNum < 4; playerNum++)
                {
                    selected.Add(ReadSelectedBro(getSelectedBroName, playerNum));
                }

                DiagnosticLog.Info(
                    "OPTIONAL_BRO_MOD trigger=" + Sanitize(trigger) +
                    "; id=Swap_Bros_Mod; installed=true; active=true" +
                    "; version=" + Sanitize(version) +
                    "; assemblyVersion=" + Sanitize(assemblyVersion) +
                    "; moduleVersionId=" + Sanitize(moduleVersionId) +
                    "; api={" + capabilities + "}" +
                    "; rosterCount=" + roster.Count +
                    "; rosterHash=" + ComputeOrderedHash(roster) +
                    "; selectedHash=" + ComputeOrderedHash(selected) +
                    "; selected=" + FormatSelected(selected) + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Optional bro mod compatibility snapshot failed: " + exception);
            }
        }

        private static MethodInfo FindPublicStaticMethod(Type type, string name, Type[] parameters)
        {
            return type == null
                ? null
                : type.GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    parameters,
                    null);
        }

        private static List<string> ReadStringList(object value)
        {
            var result = new List<string>();
            var enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return result;
            }

            foreach (var item in enumerable)
            {
                result.Add(item == null ? string.Empty : Convert.ToString(item));
            }

            return result;
        }

        private static string ReadSelectedBro(MethodInfo method, int playerNum)
        {
            if (method == null)
            {
                return "<api-unavailable>";
            }

            try
            {
                var value = method.Invoke(null, new object[] { playerNum });
                return value == null ? string.Empty : Convert.ToString(value);
            }
            catch (TargetInvocationException exception)
            {
                var cause = exception.InnerException ?? exception;
                return "<error:" + cause.GetType().Name + ">";
            }
            catch (Exception exception)
            {
                return "<error:" + exception.GetType().Name + ">";
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

        private static string ComputeOrderedHash(IList<string> values)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index] ?? string.Empty;
                builder.Append(value.Length);
                builder.Append(":");
                builder.Append(value);
                builder.Append(";");
            }

            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var result = new StringBuilder(hash.Length * 2);
                foreach (var current in hash)
                {
                    result.Append(current.ToString("x2"));
                }
                return result.ToString();
            }
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
                if (current == '\r' || current == '\n' || current == ';' || current == ',')
                {
                    builder.Append('_');
                }
                else if (char.IsControl(current))
                {
                    builder.Append('?');
                }
                else
                {
                    builder.Append(current);
                }
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }
    }
}
