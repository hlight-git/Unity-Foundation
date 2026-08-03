using System;
using UnityEngine;

namespace Hlight.Foundation
{
    public static class LayerMaskExtensions
    {
        public static LayerMask Excluding(this LayerMask source, params string[] layerNames)
        {
            if (layerNames == null) throw new ArgumentNullException(nameof(layerNames));

            var result = source.value;
            foreach (var layerName in layerNames)
            {
                var layer = LayerMask.NameToLayer(layerName);
                if (layer < 0)
                    throw new ArgumentException($"Layer '{layerName}' does not exist.", nameof(layerNames));

                result &= ~(1 << layer);
            }

            return result;
        }

        public static LayerMask AllLayersExcept(params string[] layerNames)
        {
            return ((LayerMask)Physics.AllLayers).Excluding(layerNames);
        }
    }
}
