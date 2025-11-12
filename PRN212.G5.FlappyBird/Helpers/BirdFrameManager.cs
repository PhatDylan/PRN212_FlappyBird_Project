using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FlappyBird.Business.Models;
using FlappyBird.Business.Services;

namespace PRN212.G5.FlappyBird.Helpers
{
    /// <summary>
    /// Quản lý các frame animation của bird
    /// </summary>
    public class BirdFrameManager
    {
        private BitmapImage[] dayBirdFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] dayBirdFallFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] dayBirdDeathFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdFallFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdDeathFrames = Array.Empty<BitmapImage>();

        private BitmapImage[] currentFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] currentFallFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] currentDeathFrames = Array.Empty<BitmapImage>();

        public BitmapImage[] CurrentFlyFrames => currentFlyFrames;
        public BitmapImage[] CurrentFallFrames => currentFallFrames;
        public BitmapImage[] CurrentDeathFrames => currentDeathFrames;

        /// <summary>
        /// Load tất cả các frame của bird
        /// </summary>
        public void LoadAllBirdFrames()
        {
            // Day theme - Fly animation (using single reliable frame)
            var birdFly1 = AssetHelper.LoadBitmapSafe("birdfly-1.png");
            var birdFall1 = AssetHelper.LoadBitmapSafe("birdfall-1.png");

            // Only use frames that actually exist
            if (birdFly1 != null)
            {
                dayBirdFlyFrames = new[] { birdFly1 };
            }
            else
            {
                // Fallback to a default if birdfly-1.png is missing
                dayBirdFlyFrames = new BitmapImage[0];
            }

            if (birdFall1 != null)
            {
                dayBirdFallFrames = new[] { birdFall1 };
                dayBirdDeathFrames = new[] { birdFall1 };
            }
            else
            {
                dayBirdFallFrames = new BitmapImage[0];
                dayBirdDeathFrames = new BitmapImage[0];
            }

            // Night theme - Try to load night frames, fallback to day if missing
            var birdFly3 = AssetHelper.LoadBitmapSafe("birdfly-3.png");
            var birdFall3 = AssetHelper.LoadBitmapSafe("birdfall-3.png");

            if (birdFly3 != null)
            {
                nightBirdFlyFrames = new[] { birdFly3 };
            }
            else
            {
                nightBirdFlyFrames = dayBirdFlyFrames;
            }

            if (birdFall3 != null)
            {
                nightBirdFallFrames = new[] { birdFall3 };
                nightBirdDeathFrames = new[] { birdFall3 };
            }
            else
            {
                nightBirdFallFrames = dayBirdFallFrames;
                nightBirdDeathFrames = dayBirdDeathFrames;
            }
        }

        /// <summary>
        /// Chuyển đổi frame theo theme (day/night)
        /// </summary>
        public void UseBirdFramesForTheme(bool night, BirdService birdService, Image birdImage)
        {
            currentFlyFrames = night ? nightBirdFlyFrames : dayBirdFlyFrames;
            currentFallFrames = night ? nightBirdFallFrames : dayBirdFallFrames;
            currentDeathFrames = night ? nightBirdDeathFrames : dayBirdDeathFrames;

            birdService.BirdState.FrameIndex = 0;
            birdService.BirdState.AnimationState = BirdAnimationState.Flying;

            if (currentFlyFrames.Length > 0 && currentFlyFrames[0] != null)
                birdImage.Source = currentFlyFrames[0];
        }
    }
}

