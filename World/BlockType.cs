namespace BinaryTernaryCraft.World
{
    public enum BlockType : ushort
    {
        Air = 0,
        Stone = 1,
        RGBWire = 2,
        TritSource = 3,
        NotGate = 4,
        CycleGate = 5,
        AntiCycleGate = 6,
        MinGate = 7,
        MaxGate = 8,
        SumGate = 9,
        ConsensusGate = 10,
        PulseGenerator = 11,
        PulseExtender = 12,
        ToggleLatch = 13,
        TritLatch = 14,
        RGBPixel = 15,
        KineticShaft = 16,
        KineticGear = 17,
        ElectricMotor = 18,
        TernaryPiston = 19
    }

    public static class BlockCategory
    {
        public static bool IsLogic(BlockType type)
        {
            return type is BlockType.RGBWire
                or BlockType.TritSource
                or BlockType.NotGate
                or BlockType.CycleGate
                or BlockType.AntiCycleGate
                or BlockType.MinGate
                or BlockType.MaxGate
                or BlockType.SumGate
                or BlockType.ConsensusGate
                or BlockType.PulseGenerator
                or BlockType.PulseExtender
                or BlockType.ToggleLatch
                or BlockType.TritLatch
                or BlockType.RGBPixel;
        }

        public static bool IsSimulated(BlockType type)
        {
            return IsLogic(type) || type is BlockType.ElectricMotor or BlockType.TernaryPiston;
        }

        public static bool IsKinetic(BlockType type)
        {
            return type is BlockType.KineticShaft
                or BlockType.KineticGear
                or BlockType.ElectricMotor
                or BlockType.TernaryPiston;
        }

        public static bool HasCustomGeoModel(BlockType type)
        {
            return type is BlockType.KineticShaft
                or BlockType.KineticGear
                or BlockType.ElectricMotor
                or BlockType.TernaryPiston;
        }
    }
}
