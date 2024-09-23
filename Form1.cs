using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Imaging;
using System.Data.SqlClient;
using System.Reflection;
using System.Xml.Linq;

namespace Webcam_Checker
{

    public partial class Form1 : Form
    {
        private FilterInfoCollection filterInfoCollection;
        private VideoCaptureDevice videoCaptureDevice;
        private Bitmap currentFrame;
        private bool isCapturing = false;

        public int Width { get; set; }
        public int Height { get; set; }

        //MSSQL Connection String.
        public static string SQLConnectionString = "Data Source=.; Persist Security Info=True" +
                                                    ";User ID=sa" +
                                                    ";Password=bull$dog" +
                                                    ";Initial Catalog=Webcam";

        public Form1()
        {
            InitializeComponent();
            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            videoCaptureDevice = null;
            currentFrame = new Bitmap(1280, 720); // Adjust dimensions as needed
            isCapturing = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            #region "Device Name."

            foreach (FilterInfo filterInfo in filterInfoCollection)
            {
                try
                {
                    string deviceName = GetDeviceName(filterInfo);
                    cbCamera.Items.Add(deviceName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error extracting Device name: {ex.Message}");
                }

                if (cbCamera.Items.Count > 0)
                {
                    cbCamera.SelectedIndex = 0;
                }
                else
                {
                    MessageBox.Show("No video devices detected.");
                }

                try
                {
                    if (cbCamera.SelectedIndex != -1)
                    {
                        // Get the selected camera device name
                        string selectedDeviceName = cbCamera.SelectedItem.ToString();

                        // Get the moniker string of the selected device
                        videoCaptureDevice = new VideoCaptureDevice(GetMonikerString(selectedDeviceName));

                        // Fetch available resolutions
                        var availableResolutions = videoCaptureDevice.VideoCapabilities;

                        if (availableResolutions.Length > 0)
                        {
                            // Find the resolution with the maximum width and height
                            var maxResolution = availableResolutions.OrderByDescending(res => res.FrameSize.Width * res.FrameSize.Height).FirstOrDefault();

                            // Set global Width and Height variables to the maximum resolution
                            Width = maxResolution.FrameSize.Width;
                            Height = maxResolution.FrameSize.Height;

                            // Set the device resolution to the maximum resolution
                            videoCaptureDevice.VideoResolution = maxResolution;

                            // Optionally, log the selected resolution
                            Console.WriteLine($"Selected resolution: {Width}x{Height}");
                        }

                        // Start the video capture
                        videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
                        videoCaptureDevice.Start();
                    }
                    else
                    {
                        throw new Exception("No device selected.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error initializing Video Capture Device: {ex.Message}");
                }
            }

            // Initialize currentFrame
            currentFrame = new Bitmap(Width, Height); // Adjust dimensions as needed
            #endregion
        }

        private string GetDeviceName(FilterInfo filterInfo)
        {
            return filterInfo.Name.Split(',')[0];
        }

        private string GetMonikerString(string deviceName)
        {
            foreach (FilterInfo info in filterInfoCollection)
            {
                if (info.Name.StartsWith(deviceName))
                {
                    return info.MonikerString;
                }
            }
            throw new Exception($"Device '{deviceName}' not found");
        }

        private void VideoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
            frame.RotateFlip(RotateFlipType.RotateNoneFlipX);

            // Store the brightened captured frame
            currentFrame = frame;

            pbWebcam.Image = frame;
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[cbCamera.SelectedIndex].MonikerString);
            videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
            videoCaptureDevice.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (videoCaptureDevice.IsRunning == true)
            {
                videoCaptureDevice.Stop();
                pbWebcam.Image = null;
            }

            // Check if we're currently capturing a photo
            if (isCapturing)
            {
                MessageBox.Show("Photo capture in progress. Please wait.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (videoCaptureDevice.IsRunning == true)
            {
                videoCaptureDevice.Stop();
                pbWebcam.Image = null;

                // Reset the flag
                isCapturing = false;
            }
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (currentFrame != null)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                try
                {
                    Bitmap resizedFrame = new Bitmap(Width, Height);
                    Graphics graphics = Graphics.FromImage(resizedFrame);
                    graphics.DrawImage(currentFrame, 0, 0, Width, Height);

                    // Enhance the image
                    ImageEnhancer enhancer = new ImageEnhancer();
                    Bitmap enhancedBitmap = enhancer.EnhanceImage(resizedFrame);

                    // Convert the enhanced bitmap to a byte array
                    byte[] byteArray = ImageToByteArray(enhancedBitmap);

                    // Create a MemoryStream from the byte array
                    MemoryStream memoryStream = new MemoryStream(byteArray);
                    //Using the connection to my database.
                    using (SqlConnection connection = new SqlConnection(SQLConnectionString))
                    {
                        connection.Open();
                        //Using the stored procedure to insert the image byte data into my database.
                        using (SqlCommand command = new SqlCommand("USP_INSERT_IMAGE_BYTES", connection))
                        {
                            command.CommandType = System.Data.CommandType.StoredProcedure;

                            // Convert byte array to Base64 encoded string
                            string base64String = Convert.ToBase64String(byteArray);
                            command.Parameters.Add("@ImageData", SqlDbType.VarChar).Value = base64String;

                            try
                            {
                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected < 0)
                                {
                                    MessageBox.Show($"Photo captured! Timestamp: {timestamp}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                    // Save the image locally
                                    SaveImageLocally(byteArray, timestamp);
                                }
                                else
                                {
                                    MessageBox.Show("Failed to save image to database.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            catch (SqlException ex)
                            {
                                MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }

                    // Convert byte array to hexadecimal string representation
                    string hexString = BitConverter.ToString(byteArray.Take(100).ToArray());

                    // Display the byte array in a MessageBox
                    //MessageBox.Show($"First 100 bytes of the image:\n\n{hexString}", "Image Data", MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while capturing the photo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No image available to save!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveImageLocally(byte[] byteArray, string timestamp)
        {
            try
            {
                // Get the user's Desktop folder
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Generate a unique filename
                string fileName = $"Capture_{timestamp}.png";
                string filePath = Path.Combine(documentsPath, fileName);

                // Create a Bitmap from the byte array
                using (MemoryStream ms = new MemoryStream(byteArray))
                {
                    using (Bitmap originalBitmap = new Bitmap(ms))
                    {
                        // Create a new bitmap with 24-bit color depth
                        using (Bitmap bitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, PixelFormat.Format24bppRgb))
                        {
                            // Draw the original bitmap onto the new bitmap
                            using (Graphics g = Graphics.FromImage(bitmap))
                            {
                                g.DrawImage(originalBitmap, 0, 0);
                            }

                            // Save the bitmap to a PNG file
                            bitmap.Save(filePath, ImageFormat.Png);
                        }
                    }
                }

                MessageBox.Show($"Local copy of the image saved successfully: {fileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save local copy: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private byte[] ImageToByteArray(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        // Implement this class with the following methods
        public class ImageEnhancer
        {
            public Bitmap EnhanceImage(Bitmap original)
            {
                Bitmap result = new Bitmap(original.Width, original.Height);

                for (int x = 0; x < original.Width; x++)
                {
                    for (int y = 0; y < original.Height; y++)
                    {
                        Color pixel = original.GetPixel(x, y);
                        result.SetPixel(x, y, pixel);
                    }
                }

                return result;
            }
        }
    }
}
