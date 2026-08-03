using System;
using System.Collections.Generic;

namespace Hlight.Foundation
{
    public static class EnumUtility
    {
        public static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum
        {
            return (TEnum[])Enum.GetValues(typeof(TEnum));
        }

        public static int GetValueCount<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetValues(typeof(TEnum)).Length;
        }

        public static TEnum ChooseRandom<TEnum>(params TEnum[] excludedValues)
            where TEnum : struct, Enum
        {
            var values = GetValues<TEnum>();
            if (excludedValues == null || excludedValues.Length == 0)
                return values.ChooseRandom();

            return values.ChooseRandomExcept(new HashSet<TEnum>(excludedValues));
        }
    }
}
