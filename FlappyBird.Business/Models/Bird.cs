using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlappyBird.Business.Models
{
    public class Bird
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Velocity { get; set; }
        public double Gravity { get; set; } = 500;      // pixel/s²
        public double JumpStrength { get; set; } = -250; // pixel/s

        public Bird(double x, double y)
        {
            X = x;
            Y = y;
        }

        public void Jump()
        {
            Velocity = JumpStrength;
        }

        public void Update(double dt)
        {
            Velocity += Gravity * dt;
            Y += Velocity * dt;
        }
    }
}
