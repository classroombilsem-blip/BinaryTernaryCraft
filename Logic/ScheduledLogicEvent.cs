namespace BinaryTernaryCraft.Logic
{
    public enum ScheduledEventType : byte
    {
        PulseFinish = 0,
        ExtenderFinish = 1,
        GenericStateSet = 2
    }

    public struct ScheduledLogicEvent
    {
        public int X;
        public int Y;
        public int Z;
        public sbyte TargetTrit;
        public ScheduledEventType EventType;
        public uint ExpectedStateGeneration; // For event cancellation if block state changes early

        public ScheduledLogicEvent(int x, int y, int z, sbyte targetTrit, ScheduledEventType eventType, uint expectedStateGeneration = 0)
        {
            X = x;
            Y = y;
            Z = z;
            TargetTrit = targetTrit;
            EventType = eventType;
            ExpectedStateGeneration = expectedStateGeneration;
        }
    }
}
