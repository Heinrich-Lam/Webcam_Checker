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
        private Bitmap frame;

        private bool isCapturing = false;
        private bool isFrameFrozen = false;

        //Global variable for the width and height.
        //i.e. The set resolution for the image.
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
            cbResolution.SelectedIndexChanged += cbResolution_SelectedIndexChanged;

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
                    //Calls the method to populate th Device Names into the combo.
                    string deviceName = GetDeviceName(filterInfo);
                    cbCamera.Items.Add(deviceName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error extracting Device name: {ex.Message}");
                }
            }

            if (cbCamera.Items.Count > 0)
            {
                //Sets the selected item to be the first value in the combo.
                cbCamera.SelectedIndex = 0;

                try
                {
                    // Get the selected camera device name
                    string selectedDeviceName = cbCamera.SelectedItem.ToString();

                    // Get the moniker string of the selected device
                    videoCaptureDevice = new VideoCaptureDevice(GetMonikerString(selectedDeviceName));

                    // Fetch available resolutions
                    var availableResolutions = videoCaptureDevice.VideoCapabilities;

                    // Clear existing items in cbResolution
                    cbResolution.Items.Clear();

                    //Adds this as a default value in the resolution combobox.
                    cbResolution.Items.Add("--Select Resolution--");

                    if (availableResolutions.Length > 0)
                    {
                        // Add all available resolutions to the cbResolution ComboBox
                        foreach (var resolution in availableResolutions)
                        {
                            cbResolution.Items.Add($"{resolution.FrameSize.Width} x {resolution.FrameSize.Height}");
                        }

                        // Find the resolution with the maximum width and height
                        var maxResolution = availableResolutions.OrderByDescending(res => res.FrameSize.Width * res.FrameSize.Height).FirstOrDefault();

                        // Set global Width and Height variables to the maximum resolution
                        Width = maxResolution.FrameSize.Width;
                        Height = maxResolution.FrameSize.Height;

                        // Set the device resolution to the maximum resolution
                        videoCaptureDevice.VideoResolution = maxResolution;

                        cbResolution.SelectedIndex = 0;

                    }

                    // Start the video capture
                    videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
                    videoCaptureDevice.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error initializing Video Capture Device: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("No video devices detected.");
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
            //This reads through the different Video devices and gets its unique ID.
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
            frame = (Bitmap)eventArgs.Frame.Clone();
            frame.RotateFlip(RotateFlipType.RotateNoneFlipX);

            // Store the brightened captured frame
            currentFrame = frame;

            pbWebcam.Image = frame;
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            // Get the selected resolution width and height
            var selectedResolution = videoCaptureDevice.VideoCapabilities[0]; // Assume the first one for simplicity
            Width = selectedResolution.FrameSize.Width;
            Height = selectedResolution.FrameSize.Height;

            // Reinitialize currentFrame with the correct dimensions
            currentFrame = new Bitmap(Width, Height);

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

                cbResolution.SelectedIndex = 0;
            }
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (cbResolution.SelectedIndex <= 0)
            {
                MessageBox.Show("Please select a Resolution Size!");
            }
            else
            {
                if (!isFrameFrozen)
                {
                    // Capture the frame
                    currentFrame = CaptureFrame();

                    // Freeze the frame
                    isFrameFrozen = true;

                    // Perform image processing asynchronously
                    Task.Run(() => ProcessAndSaveImage());
                }
            }
        }

        private Bitmap CaptureFrame()
        {
            // Implement your frame capture logic here
            // This could involve getting the latest frame from your webcam
            // For demonstration purposes, I'm just returning a dummy bitmap
            return new Bitmap(Width, Height);
        }

        private async void ProcessAndSaveImage()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Optimize image resizing
                Bitmap resizedFrame = new Bitmap(currentFrame.Width, currentFrame.Height);
                using (Graphics graphics = Graphics.FromImage(resizedFrame))
                    graphics.DrawImage(currentFrame, 0, 0, currentFrame.Width, currentFrame.Height);

                // Implement asynchronous image enhancement
                ImageEnhancer enhancer = new ImageEnhancer();
                Bitmap enhancedBitmap = await Task.Run(() => enhancer.EnhanceImageAsync(resizedFrame));

                // Convert enhanced bitmap to byte array asynchronously
                byte[] byteArray = await Task.Run(() => ImageToByteArray(enhancedBitmap));

                // Create a MemoryStream from the byte array asynchronously
                using (MemoryStream memoryStream = new MemoryStream(byteArray))
                {
                    // Using the connection to my database.
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
                                    // Save the image locally asynchronously
                                    await Task.Run(() => SaveImageLocally(byteArray, timestamp));

                                    MessageBox.Show($"Photo captured! Timestamp: {timestamp}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                }

                // Display the byte array in a MessageBox asynchronously
                await Task.Run(() =>
                {
                    string hexString = BitConverter.ToString(byteArray.Take(100).ToArray());
                    //MessageBox.Show($"First 100 bytes of the image:\n\n{hexString}", "Image Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while capturing the photo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Reset the frame after processing
                currentFrame = null;
                isFrameFrozen = false;
            }
        }

        private async void SaveImageLocally(byte[] byteArray, string timestamp)
        {
            try
            {
                // Get the user's Desktop folder
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Generate a unique filename
                string fileName = $"Capture_{timestamp}.jpeg";
                string filePath = Path.Combine(documentsPath, fileName);

                // Create a Bitmap from the byte array
                using (MemoryStream ms = new MemoryStream(byteArray))
                {
                    using (Bitmap originalBitmap = new Bitmap(ms))
                    {
                        // Create a new bitmap with 24-bit color depth
                        using (Bitmap bitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, PixelFormat.Format24bppRgb))
                        {
                            await Task.Delay(2500);
                            // Draw the original bitmap onto the new bitmap
                            using (Graphics g = Graphics.FromImage(bitmap))
                            {
                                g.DrawImage(originalBitmap, 0, 0);
                            }

                            // Save the bitmap to a JPEG file
                            bitmap.Save(filePath, ImageFormat.Jpeg);
                        }
                    }
                }

                //MessageBox.Show($"Local copy of the image saved successfully: {fileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                bitmap.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        // Implement this class with the following methods
        public class ImageEnhancer
        {
            public async Task<Bitmap> EnhanceImageAsync(Bitmap original)
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

        private void cbResolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbResolution.SelectedIndex > 0)
            {
                string selectedResolution = cbResolution.SelectedItem.ToString();
                string[] dimensions = selectedResolution.Split('x');

                if (dimensions.Length == 2 &&
                    int.TryParse(dimensions[0].Trim(), out int width) &&
                    int.TryParse(dimensions[1].Trim(), out int height))
                {
                    Width = width;
                    Height = height;

                    currentFrame = new Bitmap(Width, Height);

                    Console.WriteLine($"Current frame resolution set to: {Width}x{Height}");
                }
                else
                {
                    MessageBox.Show("Invalid resolution format.");
                }
            }
            else if (cbResolution.SelectedIndex == 0)
            {

            }
        }

    }
}
