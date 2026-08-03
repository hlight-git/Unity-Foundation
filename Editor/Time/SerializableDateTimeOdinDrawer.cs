using System;

#if ODIN_INSPECTOR
namespace Hlight.Foundation.Editor
{
    public sealed class SerializableDateTimeOdinDrawer : ADateTimeOdinDrawer<SerializableDateTime>
    {
        protected override DateTime Value
        {
            get => ValueEntry.SmartValue;
            set => ValueEntry.SmartValue = value;
        }
    }
}
#endif
