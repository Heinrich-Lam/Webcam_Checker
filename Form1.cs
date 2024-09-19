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
                        string selectedDeviceName = cbCamera.SelectedItem.ToString();
                        videoCaptureDevice = new VideoCaptureDevice(GetMonikerString(selectedDeviceName));

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
            currentFrame = new Bitmap(1280, 720); // Adjust dimensions as needed
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
                    // Resize the frame to 1280x760
                    int width = 1280;
                    int height = 760;
                    Bitmap resizedFrame = new Bitmap(width, height);
                    Graphics graphics = Graphics.FromImage(resizedFrame);
                    graphics.DrawImage(currentFrame, 0, 0, width, height);

                    // Enhance the image
                    ImageEnhancer enhancer = new ImageEnhancer();
                    Bitmap enhancedBitmap = enhancer.EnhanceImage(resizedFrame);

                    // Convert the enhanced bitmap to a byte array
                    byte[] byteArray = ImageToByteArray(enhancedBitmap);
                    // Convert byte array to Base64 encoded string
                    //string base64String = Convert.ToBase64String(byteArray);

                    // Create a MemoryStream from the byte array
                    MemoryStream memoryStream = new MemoryStream(byteArray);

                    using (SqlConnection connection = new SqlConnection(SQLConnectionString))
                    {
                        connection.Open();

                        using (SqlCommand command = new SqlCommand("USP_INSERT_IMAGE_BYTES", connection))
                        {
                            command.CommandType = System.Data.CommandType.StoredProcedure;

                            // Convert byte array to Base64 encoded string
                            string base64String = Convert.ToBase64String(byteArray);
                            command.Parameters.Add("@ImageData", SqlDbType.VarChar).Value = base64String;

                            try
                            {
                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    MessageBox.Show($"Image saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show($"First 100 bytes of the image:\n\n{hexString}", "Image Data", MessageBoxButtons.OK, MessageBoxIcon.Information);


                    // Now you can use memoryStream to insert into your database
                    // For example, if you're using Entity Framework:
                    // YourDbContext.YourEntity.ImageData = memoryStream.ToArray();
                    // YourDbContext.SaveChanges();

                    MessageBox.Show($"Photo captured! Timestamp: {timestamp}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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


            #region 'Picturebox Capture.'
            //if (currentFrame != null && pbWebcam.Image != null)
            //{
            //    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            //    string imagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"webcam_photo_{timestamp}.png");
            //    string byteArrayPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"webcam_bytes_{timestamp}.bin");

            //    try
            //    {
            //        // Save the frame as a PNG image
            //        currentFrame.Save(imagePath);

            //        // Convert the bitmap to a byte array
            //        byte[] byteArray = ImageToByteArray(currentFrame);

            //        // Save the byte array to a file
            //        File.WriteAllBytes(byteArrayPath, byteArray);

            //        // Display the first few bytes of the array
            //        string displayString = BitConverter.ToString(byteArray.Take(100).ToArray());
            //        MessageBox.Show($"First 100 bytes: {displayString}");

            //        MessageBox.Show($"Photo captured and saved! Timestamp: {timestamp}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    }
            //    catch (Exception ex)
            //    {
            //        MessageBox.Show($"An error occurred while capturing the photo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    }
            //}
            //else
            //{
            //    MessageBox.Show("No image available to save!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}

            //// Reset the flag
            //isCapturing = false;
            #endregion
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


    //    public partial class Form1 : Form
    //    {
    //        private FilterInfoCollection filterInfoCollection;
    //        private VideoCaptureDevice videoCaptureDevice;
    //        private Bitmap currentFrame;
    //        private bool isCapturing = false;
    //        public Form1()
    //        {
    //            InitializeComponent();
    //            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
    //            videoCaptureDevice = null;
    //            currentFrame = null;
    //            isCapturing = false;
    //        }

    //        //On form load the first selected video device will be used, and it will be started automatically.
    //        private void Form1_Load(object sender, EventArgs e)
    //        {
    //            #region "Combo Selection with default name."
    //            ////The following code will list all of the Video devices connected to the System.
    //            //filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
    //            //foreach (FilterInfo filterInfo in filterInfoCollection)
    //            //{//Adds all of the Items to the Combobox.
    //            //    cbCamera.Items.Add(filterInfo);
    //            //}
    //            //cbCamera.SelectedIndex = 0;
    //            ////This creates a new instance of the VideoCaptureDevice.
    //            //videoCaptureDevice = new VideoCaptureDevice();

    //            ////This creates a new instance of the VideoCaptureDevice.
    //            ////Also retrieves the selected object from the FilterInfoCollection, based on the currently selected index from the combobox.
    //            ////MonikerString: this contains the unique id for the selected Video Capture device.
    //            //videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[cbCamera.SelectedIndex].MonikerString);

    //            ////NewFrame is an event that fires whenever a new frame is captured from the video source.
    //            ////this line attaches the 'VideoCaptureDevice_NewFrame' method as the event handler for this event.
    //            //videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;

    //            ////Starts capturing frames from the selected video source.
    //            //videoCaptureDevice.Start();
    //            #endregion

    //            #region "Device Name."

    //            foreach (FilterInfo filterInfo in filterInfoCollection)
    //            {
    //                try
    //                {
    //                    string deviceName = GetDeviceName(filterInfo);
    //                    cbCamera.Items.Add(deviceName);
    //                }
    //                catch (Exception ex)
    //                {
    //                    MessageBox.Show($"Error extracting Device name: {ex.Message}");
    //                }

    //                if (cbCamera.Items.Count > 0)
    //                {
    //                    cbCamera.SelectedIndex = 0;
    //                }
    //                else
    //                {
    //                    MessageBox.Show("No video devices detected.");
    //                }

    //                try
    //                {
    //                    if (cbCamera.SelectedIndex != -1)
    //                    {
    //                        string selectedDeviceName = cbCamera.SelectedItem.ToString();
    //                        videoCaptureDevice = new VideoCaptureDevice(GetMonikerString(selectedDeviceName));

    //                        videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
    //                        videoCaptureDevice.Start();
    //                    }
    //                    else
    //                    {
    //                        throw new Exception("No device selected.");
    //                    }
    //                }
    //                catch (Exception ex)
    //                {
    //                    MessageBox.Show($"Error initializing Video Capture Device: {ex.Message}");
    //                }
    //            }

    //            // Initialize currentFrame
    //            currentFrame = new Bitmap(320, 240); // Adjust dimensions as needed

    //        }
    //            //Method to get all of the connected devices' names to populate the Combo.
    //            string GetDeviceName(FilterInfo filterInfo)
    //            {
    //                return filterInfo.Name.Split(',')[0];
    //            }

    //            //Also retrieves the selected object from the FilterInfoCollection, based on the currently selected index from the combobox.
    //            //MonikerString: this contains the unique id for the selected Video Capture device.
    //            string GetMonikerString(string deviceName)
    //            {
    //                foreach (FilterInfo info in filterInfoCollection)
    //                {
    //                    if (info.Name.StartsWith(deviceName))
    //                    {
    //                        //This returns the unique ID of the selected Video Capture Device.
    //                        return info.MonikerString;
    //                    }
    //                }
    //                throw new Exception($"Device '{deviceName}' not found");
    //            }

    //            #endregion

    //        }

    //        private void btnStart_Click(object sender, EventArgs e)
    //        {
    //            //This creates a new instance of the VideoCaptureDevice.
    //            //Also retrieves the selected object from the FilterInfoCollection, based on the currently selected index from the combobox.
    //            //MonikerString: this contains the unique id for the selected Video Capture device.
    //            videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[cbCamera.SelectedIndex].MonikerString);

    //            //NewFrame is an event that fires whenever a new frame is captured from the video source.
    //            //this line attaches the 'VideoCaptureDevice_NewFrame' method as the event handler for this event.
    //            videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;

    //            //Starts capturing frames from the selected video source.
    //            videoCaptureDevice.Start();
    //        }

    //        private void VideoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
    //        {
    //            //Sets the image for the display, calling the Frame to update the display in real-time.
    //            //The RotateNoneFlipX, inverts the display to ensure that the image displays correctly.
    //            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
    //            frame.RotateFlip(RotateFlipType.RotateNoneFlipX);
    //            pbWebcam.Image = frame;

    //        }

    //        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    //        {
    //            //Checking if the videoCaptureDevice is running or not.
    //            if (videoCaptureDevice.IsRunning == true)
    //            {
    //                //This stops the device from sending through images, and disposes of the resources that was captured.
    //                videoCaptureDevice.Stop();
    //                pbWebcam.Image = null;
    //            }
    //        }

    //        private void btnStop_Click(object sender, EventArgs e)
    //        {
    //            //Checking if the videoCaptureDevice is running or not.
    //            if (videoCaptureDevice.IsRunning == true)
    //            {
    //                //This stops the device from sending through images, and disposes of the resources that was captured.
    //                videoCaptureDevice.Stop();
    //                pbWebcam.Image = null;
    //            }
    //        }

    //        #region "Capture Picture From Camera."
    //        private void btnCapture_Click(object sender, EventArgs e)
    //        {
    //            if (pbWebcam.Image == null)
    //            {
    //                MessageBox.Show("No image found to save!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    //            }

    //            #region "User Set Name & Location."
    //            ////Define default values
    //            //string defaultFilename = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png";
    //            //string defaultLocation = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    //            //SaveFileDialog saveFileDialog = new SaveFileDialog();
    //            //saveFileDialog.Filter = "Image Files (*.png; *.jpg; *.jpeg)|*.png; *.jpg; *.jpeg";
    //            //saveFileDialog.Title = "Save Image As";
    //            //saveFileDialog.InitialDirectory = defaultLocation;
    //            //saveFileDialog.FileName = defaultFilename;

    //            //if (saveFileDialog.ShowDialog() == DialogResult.OK)
    //            //{
    //            //    try
    //            //    {
    //            //        string fileName = Path.GetFileName(saveFileDialog.FileName);
    //            //        string directory = Path.GetDirectoryName(saveFileDialog.FileName);

    //            //        if (!Directory.Exists(directory))
    //            //        {
    //            //            Directory.CreateDirectory(directory);
    //            //        }

    //            //        string fullPath = Path.Combine(directory, fileName);

    //            //        switch (Path.GetExtension(fileName).ToLower())
    //            //        {
    //            //            case ".jpg":
    //            //                pbWebcam.Image.Save(fullPath, ImageFormat.Jpeg);
    //            //                break;
    //            //            case ".png":
    //            //                pbWebcam.Image.Save(fullPath, ImageFormat.Png);
    //            //                break;
    //            //            default:
    //            //                MessageBox.Show("Unsupported file format. Please save as .jpg or .png", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    //            //                break;
    //            //        }
    //            //        MessageBox.Show("Image saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

    //            //    }
    //            //    catch (Exception ex)
    //            //    {

    //            //        MessageBox.Show($"An error occured while saving the image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    //            //    }
    //            //}
    //            #endregion


    //            #region "Instant Capture."
    //            ////Sets the default save location of the Image to the Desktop.
    //            //string defaultLocation = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    //            ////Sets Unique name for the image File.
    //            //string uniqueFilename = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png";
    //            //string fullPath = Path.Combine(defaultLocation, uniqueFilename);

    //            //try
    //            //{
    //            //    Directory.CreateDirectory(defaultLocation); // Ensure desktop folder exists
    //            //    //Saves the image as either a '.png' file or a '.jpg' file.
    //            //    //Default format is '.png' as it is better quality.
    //            //    switch (Path.GetExtension(uniqueFilename).ToLower())
    //            //    {
    //            //        case ".jpg":
    //            //            pbWebcam.Image.Save(fullPath, ImageFormat.Jpeg);
    //            //            break;
    //            //        case ".png":
    //            //            pbWebcam.Image.Save(fullPath, ImageFormat.Png);
    //            //            break;
    //            //        default:
    //            //            throw new ArgumentException("Unsupported file format");
    //            //    }

    //            //    MessageBox.Show($"Image '{uniqueFilename}' saved successfully to the desktop.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    //            //}
    //            //catch (Exception ex)
    //            //{
    //            //    MessageBox.Show($"An error occurred while saving the image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    //            //}
    //            #endregion

    //        }

    //        private void TakePhoto()
    //        {
    //            string imagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "webcam_photo.png");
    //            string byteArrayPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "webcam_bytes.bin");

    //            // Save the frame as a PNG image
    //            currentFrame.Save(imagePath);

    //            // Convert the bitmap to a byte array
    //            byte[] byteArray = ImageToByteArray(currentFrame);

    //            // Save the byte array to a file
    //            File.WriteAllBytes(byteArrayPath, byteArray);

    //            MessageBox.Show("Photo captured and saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    //            isCapturing = false;
    //        }

    //        private byte[] ImageToByteArray(Bitmap bitmap)
    //        {
    //            using (MemoryStream ms = new MemoryStream())
    //            {
    //                bitmap.Save(ms, ImageFormat.Png);
    //                return ms.ToArray();
    //            }
    //        }

    //        #endregion
    //    }
}
