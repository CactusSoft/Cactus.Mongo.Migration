using System.Collections.Generic;
using System.Security.Cryptography;

namespace Cactus.Mongo.Migration.Test
{
    internal static class ListExtensions
    {
        public static IList<T> Shuffle<T>(this IList<T> list)
        {
            var random = new RNGCryptoServiceProvider();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do random.GetBytes(box);
                while (!(box[0] < n * (byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            return list;
        }
    }
}
