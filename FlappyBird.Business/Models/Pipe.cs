using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlappyBird.Business.Models
{
    public class Pipe
    {
        public double X { get; set; }          // Vị trí ngang
        public double Y { get; set; }          // Vị trí dọc
        public double Width { get; set; }      // Chiều rộng ống
        public double Height { get; set; }     // Chiều cao ống
        public bool Passed { get; set; } = false; // Đã qua con chim chưa (để cộng điểm)
    }
}
