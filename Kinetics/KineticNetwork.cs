using System.Collections.Generic;

namespace BinaryTernaryCraft.Kinetics
{
    public class KineticNetwork
    {
        public int Id { get; }
        public float BaseSpeed { get; set; } = 0.0f; // Radians per second or RPM

        // Maps packed voxel coordinates to their relative gear ratio (including sign for direction)
        public Dictionary<int, float> MemberRatios { get; } = new();

        public int? DrivingMotorPackedCoord { get; set; }

        public KineticNetwork(int id)
        {
            Id = id;
        }

        public void AddMember(int packedCoord, float ratio)
        {
            MemberRatios[packedCoord] = ratio;
        }

        public float GetRatio(int packedCoord)
        {
            return MemberRatios.TryGetValue(packedCoord, out float ratio) ? ratio : 1.0f;
        }
    }
}
