using Il2CppInterop.Runtime;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AllFortified.Properties {
    internal static class Resources {
        private static readonly Assembly thisAssembly = Assembly.GetExecutingAssembly();
        private static readonly string assemblyName = thisAssembly.GetName().Name.Replace(" ", "");
        private static readonly string[] resourceNames = thisAssembly.GetManifestResourceNames();

        private static string ToFull(string resourceName) => $"{assemblyName}.Resources.{resourceName}";

        private static byte[] GetResource(string resourceName) {
            string fullName = ToFull(resourceName);

            if (!resourceNames.Contains(fullName))
                return null;

            using MemoryStream resourceStream = new();
            try {
                thisAssembly.GetManifestResourceStream(fullName).CopyTo(resourceStream);
                return resourceStream.ToArray();
            } catch {
                return null;
            }
        }

        public static AssetBundle GetAssetBundle(string resourceName) {
            byte[] data = GetResource(resourceName);
            if (data is null)
                return null;

            return AssetBundle.LoadFromMemory(data);
        }

        public static T GetResource<T>(this AssetBundle bundle, string resourceName) where T : Object {
            Object resource = bundle.LoadAsset(resourceName, Il2CppType.Of<T>());
            if (resource is null)
                return null;
            return resource.Cast<T>();
        }
    }
}
