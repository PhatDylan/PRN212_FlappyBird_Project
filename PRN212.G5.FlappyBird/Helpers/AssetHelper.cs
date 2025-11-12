using System;
using System.Windows.Media.Imaging;

namespace PRN212.G5.FlappyBird.Helpers
{
    public static class AssetHelper
    {
        /// <summary>
        /// Tạo pack URI cho asset file
        /// </summary>
        public static string Pack(string file) => $"pack://application:,,,/Assets/{file}";

        /// <summary>
        /// Tạo đường dẫn asset từ thư mục Assets
        /// </summary>
        public static string AssetPath(string file) => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", file);

        /// <summary>
        /// Load bitmap an toàn, trả về null nếu lỗi
        /// </summary>
        public static BitmapImage LoadBitmapSafe(string file)
        {
            try
            {
                return new BitmapImage(new Uri(Pack(file)));
            }
            catch
            {
                return null!;
            }
        }

        /// <summary>
        /// Lọc các frame null
        /// </summary>
        public static BitmapImage[] FilterNullFrames(BitmapImage[] frames)
        {
            var validFrames = new System.Collections.Generic.List<BitmapImage>();
            foreach (var frame in frames)
            {
                if (frame != null)
                    validFrames.Add(frame);
            }
            return validFrames.Count > 0 ? validFrames.ToArray() : frames;
        }

        /// <summary>
        /// Kiểm tra xem có frame nào bị thiếu không
        /// </summary>
        public static bool HasMissing(BitmapImage[] arr)
        {
            foreach (var image in arr)
                if (image == null) return true;
            return false;
        }
    }
}

