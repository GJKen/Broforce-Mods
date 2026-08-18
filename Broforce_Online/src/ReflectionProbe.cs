using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BroforceOnlineDiagnostics
{
    internal static class ReflectionProbe
    {
        private static readonly string[] ModeTerms = { "online", "offline", "arcade", "lobby", "multiplayer", "network" };
        private static readonly string[] NetworkTerms = { "steam", "photon", "lobby", "room", "network", "player" };

        public static string FindModeHint()
        {
            return FindTypeHint(ModeTerms, 3);
        }

        public static string FindNetworkHint()
        {
            return FindTypeHint(NetworkTerms, 2);
        }

        private static string FindTypeHint(IEnumerable<string> terms, int maxItems)
        {
            var matches = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName assemblyName;
                try
                {
                    assemblyName = assembly.GetName();
                }
                catch
                {
                    continue;
                }

                var name = assemblyName.Name ?? string.Empty;
                if (name.IndexOf("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    var typeName = type.FullName ?? type.Name;
                    if (terms.Any(term => typeName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        matches.Add(typeName);
                        if (matches.Count >= maxItems)
                        {
                            return string.Join(", ", matches.ToArray());
                        }
                    }
                }
            }

            return string.Join(", ", matches.ToArray());
        }
    }
}