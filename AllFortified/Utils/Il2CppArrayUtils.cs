using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace AllFortified.Utils {
    internal static class Il2CppArrayUtils {
        public static T[] Add<T>(this Il2CppArrayBase<T> array, params T[] values) {
            T[] result = new T[array.Length + values.Length];
            array.CopyTo(result, 0);
            values.CopyTo(result, array.Length);
            return result;
        }

        public static T[] Insert<T>(this Il2CppArrayBase<T> array, int index, params T[] values) {
            T[] added = new T[array.Length + values.Length];
            for (int i = 0; i < index; i++)
                added[i] = array[i];
            values.CopyTo(added, index);
            for (int i = index; i < array.Length; i++)
                added[i + values.Length] = array[i];
            return added;
        }
    }
}
