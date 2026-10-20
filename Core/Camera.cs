using System;
using System.Numerics;

namespace BinaryTernaryCraft.Core
{
    public class Camera
    {
        public Vector3 Position { get; set; } = new Vector3(8.0f, 20.0f, 8.0f);
        public float Yaw { get; set; } = -90.0f; // Look towards -Z
        public float Pitch { get; set; } = -20.0f;

        public float MoveSpeed { get; set; } = 15.0f;
        public float FastMoveSpeed { get; set; } = 40.0f;
        public float MouseSensitivity { get; set; } = 0.15f;

        public float AspectRatio { get; set; } = 16.0f / 9.0f;
        public float FovDegrees { get; set; } = 75.0f;
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 1000.0f;

        public Vector3 Forward { get; private set; } = -Vector3.UnitZ;
        public Vector3 Right { get; private set; } = Vector3.UnitX;
        public Vector3 Up { get; private set; } = Vector3.UnitY;

        public Camera(Vector3 startPosition, float aspectRatio)
        {
            Position = startPosition;
            AspectRatio = aspectRatio;
            UpdateVectors();
        }

        public void UpdateVectors()
        {
            float yawRad = MathF.PI / 180.0f * Yaw;
            float pitchRad = MathF.PI / 180.0f * Pitch;

            Vector3 front;
            front.X = MathF.Cos(yawRad) * MathF.Cos(pitchRad);
            front.Y = MathF.Sin(pitchRad);
            front.Z = MathF.Sin(yawRad) * MathF.Cos(pitchRad);

            Forward = Vector3.Normalize(front);
            Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
            Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
        }

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Position + Forward, Up);
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            float fovRad = MathF.PI / 180.0f * FovDegrees;
            return Matrix4x4.CreatePerspectiveFieldOfView(fovRad, AspectRatio, NearPlane, FarPlane);
        }

        public void ProcessMouseMovement(float xOffset, float yOffset, bool constrainPitch = true)
        {
            Yaw += xOffset * MouseSensitivity;
            Pitch -= yOffset * MouseSensitivity;

            if (constrainPitch)
            {
                if (Pitch > 89.0f) Pitch = 89.0f;
                if (Pitch < -89.0f) Pitch = -89.0f;
            }

            UpdateVectors();
        }
    }
}
