using System;
using System.Collections.Generic;

namespace BazaarCasino
{
    [Serializable] public class RouletteBet { public int choice, number, amount; }
    public static class RouletteBets
    {
        public static bool Validate(RouletteBet[] bets, int minimum, int maximum, uint gold, out int total)
        {
            total = 0;
            if (bets == null || bets.Length == 0 || bets.Length > 49) return false;
            long sum = 0;
            var spots = new HashSet<int>();
            foreach (var bet in bets)
            {
                if (bet == null || bet.choice < 0 || bet.choice > 12 || bet.number < 0 || bet.number > 36 || bet.amount < minimum || bet.amount > maximum || !spots.Add(bet.choice * 37 + (bet.choice == 6 ? bet.number : 0))) return false;
                sum += bet.amount;
            }
            if (sum > maximum || sum > gold) return false;
            total = (int)sum; return true;
        }
        public static int Payout(RouletteBet[] bets, int number)
        {
            long sum = 0;
            foreach (var bet in bets) sum += (long)bet.amount * Games.RouletteMultiplier(number, bet.choice, bet.number);
            return (int)Math.Min(int.MaxValue, sum);
        }
    }
    public struct BallFrame
    {
        public float time, angle, radius, wheel;
        public BallFrame(float t, double a, double r, double w) { time=t; angle=(float)a; radius=(float)r; wheel=(float)w; }
    }
    // Fixed-step polar dynamics: centrifugal support, radial gravity, deflector
    // impacts and pocket-divider restoring forces. A pocket is derived from the
    // final relative ball angle; no winning number is selected ahead of the ball.
    public sealed class RouletteSpin
    {
        public readonly List<BallFrame> frames = new List<BallFrame>();
        public int number, pocket;
        public float duration => frames[frames.Count-1].time;
        public static double Wrap(double angle) => angle - Math.Floor((angle + Math.PI) / (2*Math.PI)) * 2*Math.PI;
        public static int Pocket(double angle) => ((int)Math.Floor((Wrap(angle) + Math.PI/37) * 37/(2*Math.PI)) % 37 + 37) % 37;
        public RouletteSpin(int seed)
        {
            var rng = new Random(seed);
            const double dt = 1.0/120;
            double angle = rng.NextDouble()*2*Math.PI, speed=6.8+rng.NextDouble()*2.4;
            double radius=.94, radial=0, wheel=rng.NextDouble()*2*Math.PI;
            double wheelSpeed=-.7-rng.NextDouble()*.65;
            double nextImpact=2.7+rng.NextDouble()*.5;
            int stable=0;
            for (int step=0;step<=2400;step++)
            {
                double t=step*dt, previousWheelSpeed=wheelSpeed;
                wheelSpeed *= Math.Exp(-.24*dt); wheel+=wheelSpeed*dt;
                if (radius > .83)
                {
                    speed*=Math.Exp(-.40*dt);
                    // Bowl gravity pulls inward as centrifugal support decreases.
                    radial+=(Math.Min(.9,speed*speed*.055)-.72-radial*5)*dt;
                    radius+=radial*dt;
                    if(radius>.94) { radius=.94; radial=-Math.Abs(radial)*.55; }
                    if(t>=nextImpact && radius<.93)
                    {
                        speed*=.65+rng.NextDouble()*.22;
                        radial=.07+rng.NextDouble()*.10;
                        nextImpact=t+.23+rng.NextDouble()*.32;
                    }
                }
                else
                {
                    radius+= (.80-radius)*5*dt;
                    // Evaluate forces at the same instant for both bodies. Wheel
                    // integration above already advanced it by one fixed step.
                    double relative=Wrap(angle-wheel+wheelSpeed*dt);
                    // Divider walls repel the ball from pocket boundaries, with
                    // damped collisions; it eventually rests at a pocket centre.
                    double relativeSpeed=speed-previousWheelSpeed;
                    relativeSpeed+=(-relativeSpeed*5-2.8*Math.Sin(relative*37))*dt;
                    speed=relativeSpeed+wheelSpeed;
                    stable=Math.Abs(relativeSpeed)<.003 && Math.Abs(Math.Sin(relative*37))<.025 ? stable+1 : 0;
                }
                angle+=speed*dt;
                if(step%4==0) frames.Add(new BallFrame((float)t,angle,radius,wheel));
                if(stable>100) { frames.Add(new BallFrame((float)t,angle,radius,wheel)); break; }
            }
            var end=frames[frames.Count-1]; pocket=Pocket((double)end.angle-end.wheel); number=Games.Wheel[pocket];
        }
    }
}
