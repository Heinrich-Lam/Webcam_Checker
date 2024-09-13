using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace Webcam_Checker
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        FilterInfoCollection filterInfoCollection;
        VideoCaptureDevice videoCaptureDevice;
        //On form load the first selected video device will be used, and it will be started automatically.
        private void Form1_Load(object sender, EventArgs e)
        {
            #region "Combo Selection with default name."
            ////The following code will list all of the Video devices connected to the System.
            //filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            //foreach (FilterInfo filterInfo in filterInfoCollection)
            //{//Adds all of the Items to the Combobox.
            //    cbCamera.Items.Add(filterInfo);
            //}
            //cbCamera.SelectedIndex = 0;
            ////This creates a new instance of the VideoCaptureDevice.
            //videoCaptureDevice = new VideoCaptureDevice();

            ////This creates a new instance of the VideoCaptureDevice.
            ////Also retrieves the selected object from the FilterInfoCollection, based on the currently selected index from the combobox.
            ////MonikerString: this contains the unique id for the selected Video Capture device.
            //videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[cbCamera.SelectedIndex].MonikerString);

            ////NewFrame is an event that fires whenever a new frame is captured from the video source.
            ////this line attaches the 'VideoCaptureDevice_NewFrame' method as the event handler for this event.
            //videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;

            ////Starts capturing frames from the selected video source.
            //videoCaptureDevice.Start();
            #endregion

            #region "Device Name."

            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo filterInfo in filterInfoCollection)
            {
                try
                {
                    //Gets all of the Connected Video Devices from the system.
                    //Adds all of the Device names to the System.
                    string deviceName = GetDeviceName(filterInfo);
                    cbCamera.Items.Add(deviceName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error extracting Device name: {ex.Message}");
                }

                //Automatically selects the first device in the Combobox.
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
                    //If there is a selected device, the following will happen.
                    if (cbCamera.SelectedIndex != null)
                    {
                        //Gets the string value from the selected combo value and sends it to the 'GetMonikerString' method.
                        string selectedDeviceName = cbCamera.SelectedItem.ToString();
                        videoCaptureDevice = new VideoCaptureDevice(GetMonikerString(selectedDeviceName));
                    }
                    else
                    {
                        throw new Exception("No device selected.");
                    }

                    //NewFrame is an event that fires whenever a new frame is captured from the video source.
                    //this line attaches the 'VideoCaptureDevice_NewFrame' method as the event handler for this event.
                    videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
                    //Starts capturing frames from the selected video source.
                    videoCaptureDevice.Start();

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error initializing Video Capture Device : {ex.Message}");
                }

            }
            //Method to get all of the connected devices' names to populate the Combo.
            string GetDeviceName(FilterInfo filterInfo)
            {
                return filterInfo.Name.Split(',')[0];
            }

            //Also retrieves the selected object from the FilterInfoCollection, based on the currently selected index from the combobox.
            //MonikerString: this contains the unique id for the selected Video Capture device.
            string GetMonikerString(string deviceName)
            {
                foreach (FilterInfo info in filterInfoCollection)
                {
                    if (info.Name.StartsWith(deviceName))
                    {
                        //This returns the unique ID of the selected Video Capture Device.
                        return info.MonikerString;
                    }
                }
                throw new Exception($"Device '{deviceName}' not found");
            }

            #endregion

        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            //This creates a new instance of the VideoCaptureDevice.
            //Also retrieves the selected object from the FilterInfoCollection, based on the currently selected index from the combobox.
            //MonikerString: this contains the unique id for the selected Video Capture device.
            videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[cbCamera.SelectedIndex].MonikerString);

            //NewFrame is an event that fires whenever a new frame is captured from the video source.
            //this line attaches the 'VideoCaptureDevice_NewFrame' method as the event handler for this event.
            videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;

            //Starts capturing frames from the selected video source.
            videoCaptureDevice.Start();
        }

        private void VideoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            //Sets the image for the display, calling the Frame to update the display in real-time.
            pbWebcam.Image = (Bitmap)eventArgs.Frame.Clone();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Checking if the videoCaptureDevice is running or not.
            if (videoCaptureDevice.IsRunning == true)
            {
                //This stops the device from sending through images, and disposes of the resources that was captured.
                videoCaptureDevice.Stop();
                pbWebcam.Image = null;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            //Checking if the videoCaptureDevice is running or not.
            if (videoCaptureDevice.IsRunning == true)
            {
                //This stops the device from sending through images, and disposes of the resources that was captured.
                videoCaptureDevice.Stop();
                pbWebcam.Image = null;
            }
        }
    }
}
