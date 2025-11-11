using System;
using System.Windows;
using System.Windows.Controls;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class SettingsWindow : Window
    {
        private const double DefaultSpeed = 5.0;

        public double SelectedPipeSpeed { get; private set; } = DefaultSpeed;

        public SettingsWindow(double currentSpeed)
        {
            InitializeComponent();

            SelectedPipeSpeed = Math.Clamp(currentSpeed, PipeSpeedSlider.Minimum, PipeSpeedSlider.Maximum);
            PipeSpeedSlider.Value = SelectedPipeSpeed;
            UpdateDisplay(SelectedPipeSpeed);
        }

        private void PipeSpeedSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double rounded = Math.Round(e.NewValue, 1);
            UpdateDisplay(rounded);
        }

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            PipeSpeedSlider.Value = DefaultSpeed;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedPipeSpeed = Math.Round(PipeSpeedSlider.Value, 1);
            DialogResult = true;
            Close();
        }

        private void UpdateDisplay(double speed)
        {
            if (PipeSpeedValueText != null)
            {
                PipeSpeedValueText.Text = speed.ToString("0.0");
            }
        }
    }
}

