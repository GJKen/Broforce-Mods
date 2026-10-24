// Read the observer loaded earlier from a separate live script.
using System;
using System.Reflection;

public static class MaskInitObserverStatusLive
{
    public static void Main()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type observer = assembly.GetType("MaskInitObserverLive", false);
            if (observer == null) continue;
            int calls = (int)observer.GetMethod("GetCalls", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
            ScriptContext.Logger("OBSERVER_STATUS|calls=" + calls);
            return;
        }
        throw new Exception("Mask init observer assembly not found");
    }
}
