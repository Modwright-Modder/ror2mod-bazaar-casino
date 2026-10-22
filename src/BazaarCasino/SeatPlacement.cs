using System;

namespace BazaarCasino
{
    public static class SeatPlacement
    {
        public static bool PauseMovement(bool seated,bool alive,bool inBazaar,bool sceneExiting)
            => seated&&alive&&inBazaar&&!sceneExiting;
        public static bool Accept(uint body, uint currentBody, int slot, bool rootReturn, double x, double y, double z)
            => body != 0 && body == currentBody && slot >= 0 && slot < 8 && rootReturn && Finite(x) && Finite(y) && Finite(z);

        // Native footPosition is the root for Rigidbody survivors (e.g. Tank).
        // Placing that root on the floor embeds its collider in the platform.
        public static float RootHeight(float capsuleHeight, float colliderBottomOffset)
        {
            float height = capsuleHeight > 0 && Finite(capsuleHeight) ? capsuleHeight * .5f : colliderBottomOffset;
            return Finite(height) ? Math.Max(0, Math.Min(12, height)) + .12f : .12f;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
