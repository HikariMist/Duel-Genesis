using System.Collections.Generic;

namespace DuelGenesis.Dueling
{
    internal static class ReadOnlyListExtensions
    {
        public static bool Contains<T>(this IReadOnlyList<T> source, T item)
        {
            if (source == null)
                return false;

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < source.Count; i++)
            {
                if (comparer.Equals(source[i], item))
                    return true;
            }

            return false;
        }
    }
}
