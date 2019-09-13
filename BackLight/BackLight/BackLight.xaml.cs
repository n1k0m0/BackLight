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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Drawing.Rectangle;
using Timer = System.Windows.Forms.Timer;

namespace BackLight
{
    /// <summary>
    /// logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UdpClient _udpClient = new UdpClient();
        private List<System.Windows.Media.Color> _upperColors = new List<System.Windows.Media.Color>();
        private List<System.Windows.Media.Color> _lowerColors = new List<System.Windows.Media.Color>();
        private List<System.Windows.Media.Color> _leftColors = new List<System.Windows.Media.Color>();
        private List<System.Windows.Media.Color> _rightColors = new List<System.Windows.Media.Color>();
        private Timer _timer = new Timer();

        public MainWindow()
        {
            InitializeComponent();
            Visibility = Visibility.Hidden;            
            _timer.Interval = 1000 / Properties.Settings.Default.UpdateRate;
            _timer.Tick += timer_Tick;
            _timer.Enabled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer_Tick(object sender, EventArgs e)
        {
            _timer.Interval = 1000 / Properties.Settings.Default.UpdateRate;
            Dispatcher.Invoke(() =>
               {
                   try
                   {
                       GenerateAndSendBacklightPixels();
                   }
                   catch (Exception)
                   {
                       //do nothing
                   }
               });
        }

        /// <summary>
        /// Generates upper, lower, left, and right pixel data
        /// and sends these to the ESPServer for displaying it
        /// </summary>
        private void GenerateAndSendBacklightPixels()
        {            
            var screenshot = GenerateScreenshot();

            DrawingVisual visual = null;
            DrawingContext context = null;            

            if (Constants.DebugDraw)
            {
                visual = new DrawingVisual();
                context = visual.RenderOpen();
            }

            //get all relevant data from the screenshot
            BitmapData srcData = screenshot.LockBits(
                new Rectangle(0, 0, screenshot.Width, screenshot.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = srcData.Stride;
                IntPtr scan0 = srcData.Scan0;

                //here, we generate the actual pixel data for the strip
                _upperColors.Clear();
                GenerateUpperRow(scan0, stride, context);
                _lowerColors.Clear();
                GenerateLowerRow(scan0, stride, context);
                _leftColors.Clear();
                GenerateLeftColumn(scan0, stride, context);
                _rightColors.Clear();
                GenerateRightColumn(scan0, stride, context);
            }
            finally
            {
                screenshot.UnlockBits(srcData);
            }

            //if the user selected static color, we overwrite all colors by the static one
            if (Constants.StaticColor)
            {
                var color = System.Windows.Media.Color.FromRgb(Properties.Settings.Default.StaticColor.R, Properties.Settings.Default.StaticColor.G, Properties.Settings.Default.StaticColor.B);
                SetAllToOneColor(color);
            }

            //Create the bitmap and render the rectangles onto it
            //should only be used for debug purposes
            if (context != null)
            {
                context.Close();
                RenderTargetBitmap bmp = new RenderTargetBitmap(1900, 1080, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(visual);
                Image.Source = bmp;
            }

            //Send pixels and checksum to the server
            SendPixels(_upperColors, _lowerColors, _leftColors, _rightColors);

            GC.Collect();
        }

        /// <summary>
        /// Overwrites all color values
        /// </summary>
        /// <param name="color"></param>
        private void SetAllToOneColor(System.Windows.Media.Color color)
        {            
            int count = _upperColors.Count;
            _upperColors.Clear();
            for (int i = 0; i < count; i++)
            {
                _upperColors.Add(color);
            }
            count = _lowerColors.Count;
            _lowerColors.Clear();
            for (int i = 0; i < count; i++)
            {
                _lowerColors.Add(color);
            }
            count = _leftColors.Count;
            _leftColors.Clear();
            for (int i = 0; i < count; i++)
            {
                _leftColors.Add(color);
            }
            count = _rightColors.Count;
            _rightColors.Clear();
            for (int i = 0; i < count; i++)
            {
                _rightColors.Add(color);
            }
        }

        /// <summary>
        /// Generates the upper strip row
        /// </summary>
        /// <param name="scan0"></param>
        /// <param name="stride"></param>
        /// <param name="context"></param>
        private void GenerateUpperRow(IntPtr scan0, int stride, DrawingContext context = null)
        {
            for (int i = 0; i < 43; i++)
            {
                long[] totals = new long[] { 0, 0, 0 };
                double pixels = 0;
                //Step 1: sum all pixel information
                unsafe
                {
                    byte* p = (byte*)(void*)scan0;

                    for (int y = Constants.verticalOffset; y < Constants.verticalOffset + Constants.scanPixelSize; y++)
                    {
                        for (int x = i * 44; x < (i + 1) * 44; x++)
                        {
                            pixels++;
                            for (int color = 0; color < 3; color++)
                            {
                                int idx = (y * stride) + x * 4 + color;
                                totals[color] += p[idx];
                            }
                        }
                    }
                }

                //Step 2: calculate average color
                byte avgB = (byte)(totals[0] / pixels);
                byte avgG = (byte)(totals[1] / pixels);
                byte avgR = (byte)(totals[2] / pixels);

                //Step 3: add to our upper color list
                System.Windows.Media.Color avgColor = System.Windows.Media.Color.FromRgb(avgR, avgG, avgB);
                _upperColors.Add(avgColor);

                //Optional step 4: draw the rectangle
                if (context != null)
                {
                    var brush = new SolidColorBrush(avgColor);
                    context.DrawRectangle(brush, null, new Rect(i * 44, 0, 44, Constants.scanPixelSize * 2));
                }
            }
        }

        /// <summary>
        /// Generates the lower strip row
        /// </summary>
        /// <param name="scan0"></param>
        /// <param name="stride"></param>
        /// <param name="context"></param>
        private void GenerateLowerRow(IntPtr scan0, int stride, DrawingContext context = null)
        {
            for (int i = 0; i < 43; i++)
            {
                long[] totals = new long[] { 0, 0, 0 };
                double pixels = 0;
                //Step 1: sum all pixel information
                unsafe
                {
                    byte* p = (byte*)(void*)scan0;

                    for (int y = 1080 - Constants.verticalOffset - Constants.scanPixelSize; y < 1080 - Constants.verticalOffset; y++)
                    {
                        for (int x = i * 44; x < (i + 1) * 44; x++)
                        {
                            pixels++;
                            for (int color = 0; color < 3; color++)
                            {
                                int idx = (y * stride) + x * 4 + color;
                                totals[color] += p[idx];
                            }
                        }
                    }
                }

                //Step 2: calculate average color
                byte avgB = (byte)(totals[0] / pixels);
                byte avgG = (byte)(totals[1] / pixels);
                byte avgR = (byte)(totals[2] / pixels);

                //Step 3: add to our lower color list
                System.Windows.Media.Color avgColor = System.Windows.Media.Color.FromRgb(avgR, avgG, avgB);
                _lowerColors.Add(avgColor);

                //Optional step 4: draw the rectangle
                if (context != null)
                {
                    var brush = new SolidColorBrush(avgColor);
                    context.DrawRectangle(brush, null, new Rect(i * 44, 1080 - Constants.scanPixelSize * 2, 44, Constants.scanPixelSize * 2));
                }
            }
        }

        /// <summary>
        /// Generates the left strip column
        /// </summary>
        /// <param name="scan0"></param>
        /// <param name="stride"></param>
        /// <param name="context"></param>
        private void GenerateLeftColumn(IntPtr scan0, int stride, DrawingContext context = null)
        {
            for (int i = 0; i < 24; i++)
            {
                long[] totals = new long[] { 0, 0, 0 };
                double pixels = 0;
                //Step 1: sum all pixel information
                unsafe
                {
                    byte* p = (byte*)(void*)scan0;

                    for (int y = i * 45; y < (i + 1) * 45; y++)
                    {
                        for (int x = 0; x < Constants.scanPixelSize; x++)
                        {
                            pixels++;
                            for (int color = 0; color < 3; color++)
                            {
                                int idx = (y * stride) + x * 4 + color;
                                totals[color] += p[idx];
                            }
                        }
                    }
                }

                //Step 2: calculate average color
                byte avgB = (byte)(totals[0] / pixels);
                byte avgG = (byte)(totals[1] / pixels);
                byte avgR = (byte)(totals[2] / pixels);

                //Step 3: add to our left color list
                System.Windows.Media.Color avgColor = System.Windows.Media.Color.FromRgb(avgR, avgG, avgB);
                _leftColors.Add(avgColor);

                //Optional step 4: draw the rectangle
                if (context != null)
                {
                    var brush = new SolidColorBrush(avgColor);
                    context.DrawRectangle(brush, null, new Rect(0, i * 45, Constants.scanPixelSize * 2, 45));
                }
            }
        }

        /// <summary>
        /// Generate the right strip column
        /// </summary>
        /// <param name="scan0"></param>
        /// <param name="stride"></param>
        /// <param name="context"></param>
        private void GenerateRightColumn(IntPtr scan0, int stride, DrawingContext context = null)
        {
            for (int i = 0; i < 24; i++)
            {
                long[] totals = new long[] { 0, 0, 0 };
                double pixels = 0;
                //Step 1: sum all pixel information
                unsafe
                {
                    byte* p = (byte*)(void*)scan0;

                    for (int y = i * 45; y < (i + 1) * 45; y++)
                    {
                        for (int x = 1900 - Constants.scanPixelSize; x < 1900; x++)
                        {
                            pixels++;
                            for (int color = 0; color < 3; color++)
                            {
                                int idx = (y * stride) + x * 4 + color;
                                totals[color] += p[idx];
                            }
                        }
                    }
                }

                //Step 2: calculate average color
                byte avgB = (byte)(totals[0] / pixels);
                byte avgG = (byte)(totals[1] / pixels);
                byte avgR = (byte)(totals[2] / pixels);

                //Step 3: add to our left color list
                System.Windows.Media.Color avgColor = System.Windows.Media.Color.FromRgb(avgR, avgG, avgB);
                _rightColors.Add(avgColor);

                //Optional step 4: draw the rectangle
                if (context != null)
                {
                    var brush = new SolidColorBrush(avgColor);
                    context.DrawRectangle(brush, null, new Rect(1900 - Constants.scanPixelSize * 2, i * 45, Constants.scanPixelSize * 2, 45));
                }
            }
        }

        /// <summary>
        /// Sends all strip pixels (including a checksum) as udp packet to the defined ip address
        /// </summary>
        /// <param name="upperColors"></param>
        /// <param name="lowerColors"></param>
        /// <param name="leftColors"></param>
        /// <param name="rightColors"></param>
        private void SendPixels(List<System.Windows.Media.Color> upperColors, List<System.Windows.Media.Color> lowerColors, List<System.Windows.Media.Color> leftColors, List<System.Windows.Media.Color> rightColors)
        {
            byte[] data = new byte[1 + 48 * 5 + 86 * 5 + 48 * 5 + 86 * 5 + 2];
            data[0] = (byte)Properties.Settings.Default.Brightness;
            
            //1. left
            for (short offset = 0; offset < 48; offset+=2)
            {
                data[offset * 5 + 1] = (byte)(offset & 0x00FF); //pixelLo
                data[offset * 5 + 2] = (byte)((offset & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 3] = leftColors[23 - offset / 2].R;
                data[offset * 5 + 4] = leftColors[23 - offset / 2].G;
                data[offset * 5 + 5] = leftColors[23 - offset / 2].B;

                data[offset * 5 + 6] = (byte)((offset + 1) & 0x00FF); //pixelLo
                data[offset * 5 + 7] = (byte)(((offset + 1) & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 8] = leftColors[23 - offset / 2].R;
                data[offset * 5 + 9] = leftColors[23 - offset / 2].G;
                data[offset * 5 + 10] = leftColors[23 - offset / 2].B;
            }

            //2. upper
            for (short offset = 0; offset < 86; offset += 2)
            {
                data[offset * 5 + 1 + 240] = (byte)((offset + 48) & 0x00FF); //pixelLo
                data[offset * 5 + 2 + 240] = (byte)(((offset + 48) & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 3 + 240] = upperColors[offset / 2].R;
                data[offset * 5 + 4 + 240] = upperColors[offset / 2].G;
                data[offset * 5 + 5 + 240] = upperColors[offset / 2].B;

                data[offset * 5 + 6 + 240] = (byte)((offset + 48 + 1) & 0x00FF); //pixelLo
                data[offset * 5 + 7 + 240] = (byte)(((offset + 48 + 1) & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 8 + 240] = upperColors[offset / 2].R;
                data[offset * 5 + 9 + 240] = upperColors[offset / 2].G;
                data[offset * 5 + 10 + 240] = upperColors[offset / 2].B;
            }

            //3. right
            for (short offset = 0; offset < 48; offset += 2)
            {
                data[offset * 5 + 1 + 670] = (byte)((offset + 134) & 0x00FF); //pixelLo
                data[offset * 5 + 2 + 670] = (byte)(((offset + 134) & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 3 + 670] = rightColors[offset / 2].R;
                data[offset * 5 + 4 + 670] = rightColors[offset / 2].G;
                data[offset * 5 + 5 + 670] = rightColors[offset / 2].B;

                data[offset * 5 + 6 + 670] = (byte)((offset + 134 + 1) & 0x00FF); //pixelLo
                data[offset * 5 + 7 + 670] = (byte)(((offset + 134 + 1) & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 8 + 670] = rightColors[offset / 2].R;
                data[offset * 5 + 9 + 670] = rightColors[offset / 2].G;
                data[offset * 5 + 10 + 670] = rightColors[offset / 2].B;
            }

            //4. lower
            for (short offset = 0; offset < 86; offset += 2)
            {
                data[offset * 5 + 1 + 910] = (byte)((offset + 182) & 0x00FF); //pixelLo
                data[offset * 5 + 2 + 910] = (byte)(((offset + 182) & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 3 + 910] = lowerColors[42 - offset / 2].R;
                data[offset * 5 + 4 + 910] = lowerColors[42 - offset / 2].G;
                data[offset * 5 + 5 + 910] = lowerColors[42 - offset / 2].B;

                data[offset * 5 + 6 + 910] = (byte)((offset + 182 + 1) & 0x00FF); //pixelLo
                data[offset * 5 + 7 + 910] = (byte)(((offset + 182 + 1) & 0xFF00) >> 8); // pixelHi
                data[offset * 5 + 8 + 910] = lowerColors[42 - offset / 2].R;
                data[offset * 5 + 9 + 910] = lowerColors[42 - offset / 2].G;
                data[offset * 5 + 10 + 910] = lowerColors[42 - offset / 2].B;
            }
            
            //5. calculate checksum over all pixel data
            byte[] checksum = CalculateChecksum(data);
            data[data.Length - 2] = checksum[0];
            data[data.Length - 1] = checksum[1];

            IPAddress ipAddress = IPAddress.Parse(Properties.Settings.Default.IpAddress);
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, Properties.Settings.Default.Port);
            _udpClient.Send(data, data.Length, ipEndPoint);
        }

        /// <summary>
        /// Fletcher-16 checksum
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private byte[] CalculateChecksum(byte[] data)
        {
            byte sum1 = 0;
            byte sum2 = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum1 = (byte)((sum1 + data[i]) % 255);
                sum2 = (byte)((sum2 + sum1) % 255);
            }
            return new byte[] { sum1, sum2 };
        }

        /// <summary>
        /// Helper method to generate a screenshot
        /// </summary>
        /// <returns></returns>
        private Bitmap GenerateScreenshot()
        {
            //Create a new bitmap
            var screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

            // Create a graphics object from the bitmap
            var g = Graphics.FromImage(screenshot);

            // Take the screenshot from the upper left corner to the right bottom corner
            g.CopyFromScreen(0,0,0,0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);          

            return screenshot;
        }

        /// <summary>
        /// Shows and hides the user interface
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_ShowHideUiClick(object sender, RoutedEventArgs e)
        {
            if (this.Visibility == Visibility.Hidden)
            {
                Visibility = Visibility.Visible;
                Constants.DebugDraw = true;
            }
            else
            {
                Visibility = Visibility.Hidden;
                Constants.DebugDraw = false;
            }
        }

        /// <summary>
        /// By clicking the context menu quit, the user closes the application...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_QuitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// By clicking the context menu quit, the user closes the application...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_ShowSettingsClick(object sender, RoutedEventArgs e)
        {
            if (Settings.CurrentSettings == null)
            {
                Settings.CurrentSettings = new Settings();
                Settings.CurrentSettings.ShowInTaskbar = false;
                Settings.CurrentSettings.Show();
            }
        }

        /// <summary>
        /// Toggles between static and non-static color mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StaticColorMode_Click(object sender, RoutedEventArgs e)
        {
            var item = (System.Windows.Controls.MenuItem)sender;
            if (item.IsChecked)
            {
                Constants.StaticColor = true;
            }
            else
            {
                Constants.StaticColor = false;
            }
        }

        /// <summary>
        /// When we close the window, we also send a "dark packet" to shut down the LED strip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            Thread.Sleep(100); //give the last tick of the timer some time to be finished
            //then, send the "dark packet" with all colors set to black
            SetAllToOneColor(Colors.Black);
            SendPixels(_upperColors, _lowerColors, _leftColors, _rightColors);
        }
    }
}
