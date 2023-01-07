using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace AllFortified.Utils {
    internal static class ModelUtils {
        public static T CloneCast<T>(this T model) where T : Model => model.Clone().Cast<T>();

        public static T FirstBehavior<T>(this BloonModel bloon) where T : Model => FirstBehavior<T, Model>(bloon.behaviors);

        public static T GetBehavior<T>(this BloonModel bloon, int n) where T : Model => GetBehavior<T, Model>(bloon.behaviors, n);

        private static T FirstBehavior<T, B>(Il2CppReferenceArray<B> behaviors) where T : B where B : Model {
            foreach (Model behavior in behaviors) {
                if (Il2CppType.Of<T>().IsAssignableFrom(behavior.GetIl2CppType()))
                    return behavior.Cast<T>();
            }
            return null;
        }

        private static T GetBehavior<T, B>(Il2CppReferenceArray<B> behaviors, int n) where T : B where B : Model {
            int i = 0;
            foreach (Model behavior in behaviors) {
                if (Il2CppType.Of<T>().IsAssignableFrom(behavior.GetIl2CppType())) {
                    if (i == n)
                        return behavior.Cast<T>();
                    i++;
                }
            }
            return null;
        }
    }
}
