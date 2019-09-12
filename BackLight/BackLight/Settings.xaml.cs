/*                              
   Copyright 2019, Nils Kopal, nils<at>kopaldev.de

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace BackLight
{
    /// <summary>
    /// Interaktionslogik für Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {        
        public Settings()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            StaticColor.SelectedColor = DrawingToMedia(Properties.Settings.Default.StaticColor);
            UpdateRate.Text = "" + Properties.Settings.Default.UpdateRate;
            IpAddress.Text = Properties.Settings.Default.IpAddress;
            Port.Text = "" + Properties.Settings.Default.Port;
            BrightnessSlider.Value = Properties.Settings.Default.Brightness;
        }

        /// <summary>
        /// I use this to detect, if there is already an open settings window
        /// </summary>
        public static Settings CurrentSettings
        {
            get;
            set;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CurrentSettings = null;
        }

        private void StaticColor_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            SaveButton.IsEnabled = true;
        }

        private void UpdateRate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }

        private void IpAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }

        private void Port_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            int rate;
            int port;

            //1. some checks for correct ip address, port, etc
            try
            {
                rate = int.Parse(UpdateRate.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Invalid update rate:" + UpdateRate.Text, "Invalid update rate", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                IPAddress.Parse(IpAddress.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Invalid IP address:" + IpAddress.Text, "Invalid IP address", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }            
            try
            {
                port = int.Parse(Port.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Invalid port:" + Port.Text, "Invalid port", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (port < 0 || port > 65535)
            {
                MessageBox.Show("Invalid port (it has to be between 0 and 65535):" + Port.Text, "Invalid port", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            //2. save everything
            Properties.Settings.Default.StaticColor = MediaToDrawing(StaticColor.SelectedColor);
            Properties.Settings.Default.UpdateRate = rate;
            Properties.Settings.Default.IpAddress = IpAddress.Text;
            Properties.Settings.Default.Port = port;
            Properties.Settings.Default.Brightness = (int)BrightnessSlider.Value;
            Properties.Settings.Default.Save();
            SaveButton.IsEnabled = false;
        }

        #region some color converter functions

        private System.Drawing.Color MediaToDrawing(System.Windows.Media.Color? selectedColor)
        {
            if(selectedColor is System.Windows.Media.Color)
            {
                return MediaToDrawing((System.Windows.Media.Color)selectedColor);
            }
            return System.Drawing.Color.White;
        }

        public static System.Windows.Media.Color DrawingToMedia(System.Drawing.Color color)
        {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Drawing.Color MediaToDrawing(System.Windows.Media.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SaveButton.IsEnabled = true;
        }

        #endregion
    }
}
