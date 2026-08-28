namespace BroforceOnlineDiagnostics
{
    // The standard build script supplies the implementation with a compile-time hash.
    // Keeping the fallback makes source-only IDE builds identifiable as untracked builds.
    internal static partial class BuildMetadata
    {
        private static readonly string HashValue = ResolveHash();

        internal static string BuildHash
        {
            get { return HashValue; }
        }

        private static string ResolveHash()
        {
            var value = "UNBUILT";
            SetBuildHash(ref value);
            return value;
        }

        static partial void SetBuildHash(ref string value);
    }
}
