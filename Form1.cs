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
using System.Threading;

namespace Webcam_Checker
{

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            cbResolution.SelectedIndexChanged += cbResolution_SelectedIndexChanged;

            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            videoCaptureDevice = null;
            currentFrame = new Bitmap(1280, 720); // Adjust dimensions as needed
            isCapturing = false;

            btnCapture.Enabled = false;
        }

        #region "Global Variables."
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

        public int originalWidth;
        public int originalHeight;

        //MSSQL Connection String.
        public static string SQLConnectionString = "Data Source=.; Persist Security Info=True" +
                                                    ";User ID=sa" +
                                                    ";Password=bull$dog" +
                                                    ";Initial Catalog=Webcam";

        Thread InitialLoad = null;
        bool isRunning = false;

        Thread InitialCapture = null;
        bool isCapture = false;

        #endregion

        #region "Initial Load."
        private void Form1_Load(object sender, EventArgs e)
        {
            LoadVideo();
        }

        private void LoadVideo()
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
                        //// Filter out resolutions smaller than 640x360
                        //var filteredResolutions = availableResolutions
                        //    .Where(res => res.FrameSize.Width >= 640 && res.FrameSize.Height >= 360);

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
            // Always update currentFrame with the latest frame
            currentFrame = (Bitmap)eventArgs.Frame.Clone();
            currentFrame.RotateFlip(RotateFlipType.RotateNoneFlipX);

            // Only update PictureBox if not frozen
            if (!isFrameFrozen)
            {
                pbWebcam.Image = (Bitmap)currentFrame.Clone();  // Display current frame
            }

            // Enable capture button after the first frame is ready
            if (!btnCapture.Enabled)
            {
                btnCapture.Invoke((MethodInvoker)(() => btnCapture.Enabled = true));
            }
        }
        #endregion

        #region "Close and clear Resources."
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

        #endregion

        #region "Restart Camera."
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
        #endregion

        #region "Stop Camera."
        private void btnStop_Click(object sender, EventArgs e)
        {
            StopVideoOutput();
        }

        private void StopVideoOutput()
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

        #endregion

        #region "Take Picture."
        private void btnCapture_Click(object sender, EventArgs e)
        {
            CaptureImage();
        }

        private void CaptureImage()
        {
            if (cbResolution.SelectedIndex <= 0)
            {
                MessageBox.Show("Please select a Resolution Size!");
            }
            else
            {
                if (!isFrameFrozen && currentFrame != null)
                {
                    // Freeze the frame (only stops updating PictureBox, not the frame capture)
                    isFrameFrozen = true;

                    // Perform image processing asynchronously using the current frame
                    Task.Run(() => ProcessAndSaveImage((Bitmap)currentFrame.Clone()));
                }
                else
                {
                    MessageBox.Show("No valid frame available for capture.");
                }
            }
        }

        //private Bitmap CaptureFrame()
        //{
        //    // Implement your frame capture logic here
        //    // This could involve getting the latest frame from your webcam
        //    // For demonstration purposes, I'm just returning a dummy bitmap
        //    return new Bitmap(Width, Height);
        //}
        #region "Process Image to insert into Database."
        private async void ProcessAndSaveImage(Bitmap capturedFrame)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Optimize image resizing
                Bitmap resizedFrame = new Bitmap(capturedFrame.Width, capturedFrame.Height);
                using (Graphics graphics = Graphics.FromImage(resizedFrame))
                {
                    graphics.DrawImage(capturedFrame, 0, 0, capturedFrame.Width, capturedFrame.Height);
                }

                // Implement asynchronous image enhancement
                ImageEnhancer enhancer = new ImageEnhancer();
                Bitmap enhancedBitmap = await Task.Run(() => enhancer.EnhanceImageAsync(resizedFrame));

                // Convert enhanced bitmap to byte array asynchronously
                byte[] byteArray = await Task.Run(() => ImageToByteArray(enhancedBitmap));

                // Save the byte array to the database asynchronously
                using (MemoryStream memoryStream = new MemoryStream(byteArray))
                {
                    using (SqlConnection connection = new SqlConnection(SQLConnectionString))
                    {
                        connection.Open();

                        using (SqlCommand command = new SqlCommand("USP_INSERT_IMAGE_BYTES", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;

                            // Convert byte array to Base64 encoded string
                            string base64String = Convert.ToBase64String(byteArray);
                            command.Parameters.Add("@ImageData", SqlDbType.VarChar).Value = base64String;

                            try
                            {
                                int rowsAffected = await command.ExecuteNonQueryAsync();

                                if (rowsAffected < 0)
                                {
                                    // If the database insert fails, save the image locally
                                    await Task.Run(() => SaveImageLocally(byteArray, timestamp));
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

                // Display the first 100 bytes of the image in a MessageBox asynchronously
                await Task.Run(() =>
                {
                    string hexString = BitConverter.ToString(byteArray.Take(100).ToArray());
                    // Optionally display the hex string if needed for debugging purposes
                    // MessageBox.Show($"First 100 bytes of the image:\n\n{hexString}", "Image Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while capturing the photo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Reset the frozen frame flag after processing is complete
                isFrameFrozen = false;
            }
        }
        #endregion

        #region "Save Image Locally."
        private async void SaveImageLocally(byte[] byteArray, string timestamp)
        {
            try
            {
                // Get the user's Desktop folder path
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Generate a unique filename using the timestamp
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
                            // Optional delay for testing purposes
                            await Task.Delay(2500);

                            // Draw the original bitmap onto the new bitmap
                            using (Graphics g = Graphics.FromImage(bitmap))
                            {
                                g.DrawImage(originalBitmap, 0, 0);
                            }

                            // Save the bitmap to a JPEG file locally
                            bitmap.Save(filePath, ImageFormat.Jpeg);
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
        #endregion

        #region "Convert Image to Byte Array."
        private byte[] ImageToByteArray(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }
        #endregion
        #region "Enhance Image."
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
        #endregion
        #region "Resolution Selection."
        private void cbResolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbResolution.SelectedIndex > 0)
            {
                // Stop the video capture
                if (videoCaptureDevice.IsRunning)
                {
                    videoCaptureDevice.Stop();
                }

                // Extract the selected resolution from the ComboBox
                string selectedResolution = cbResolution.SelectedItem.ToString();
                string[] dimensions = selectedResolution.Split('x');

                if (dimensions.Length == 2 &&
                    int.TryParse(dimensions[0].Trim(), out int width) &&
                    int.TryParse(dimensions[1].Trim(), out int height))
                {
                    Width = width;
                    Height = height;

                    // Set the new frame size for the currentFrame bitmap
                    currentFrame = new Bitmap(Width, Height);

                    // Set the new resolution on the video capture device
                    var selectedResolutionCapability = videoCaptureDevice.VideoCapabilities
                        .FirstOrDefault(res => res.FrameSize.Width == Width && res.FrameSize.Height == Height);

                    if (selectedResolutionCapability != null)
                    {
                        videoCaptureDevice.VideoResolution = selectedResolutionCapability;
                    }

                    // Restart the video capture with the new resolution
                    videoCaptureDevice.Start();

                    Console.WriteLine($"Resolution changed to: {Width}x{Height}");
                }
                else
                {
                    MessageBox.Show("Invalid resolution format.");
                }
            }
            else if (cbResolution.SelectedIndex == 0)
            {
                MessageBox.Show("Please select a valid resolution.");
            }
        }
        #endregion
        #endregion
    }
}
