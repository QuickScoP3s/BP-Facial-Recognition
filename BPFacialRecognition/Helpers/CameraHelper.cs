using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace BPFacialRecognition.Helpers {
    /// <summary>
    /// Interacts with an attached camera. Allows one to easily access live webcam feed and capture a photo.
    /// </summary>
    public class CameraHelper {

        public MediaCapture MediaCapture { get; private set; }

        public bool Initialized { get; private set; } = false;

        /// <summary>
        /// Asynchronously initializes webcam feed
        /// </summary>
        public async Task InitializeCameraAsync() {
            if (MediaCapture == null) {

                var cameraDevice = await FindCameraDevice();
                if (cameraDevice == null) {
                    // No camera found, report the error and break out of initialization
                    Debug.WriteLine("No camera found!");
                    Initialized = false;
                    return;
                }

                // Creates MediaCapture initialization settings with foudnd webcam device
                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                MediaCapture = new MediaCapture();
                await MediaCapture.InitializeAsync(settings);

                Initialized = true;
            }
        }

        /// <summary>
        /// Asynchronously looks for and returns first camera device found.
        /// If no device is found, return null
        /// </summary>
        private static async Task<DeviceInformation> FindCameraDevice() {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            if (devices.Count < 1)
                return null;

            // Find MS LifeCam
            var lifecam = devices.FirstOrDefault(x => x.Name.Contains("LifeCam"));
            if (lifecam != null)
                return lifecam;

            return devices[0];
        }

        /// <summary>
        /// Asynchronously begins live webcam feed
        /// </summary>
        public async Task StartCameraPreview() {
            try {
                await MediaCapture.StartPreviewAsync();
            }
            catch {
                Initialized = false;
                Debug.WriteLine("Failed to start camera preview stream");

            }
        }

        /// <summary>
        /// Asynchronously ends live webcam feed
        /// </summary>
        public async Task StopCameraPreview() {
            try {
                await MediaCapture.StopPreviewAsync();
            }
            catch {
                Debug.WriteLine("Failed to stop camera preview stream");
            }
        }


        /// <summary>
        /// Asynchronously captures photo from camera feed and stores it in local storage. Returns image file as a StorageFile.
        /// File is stored in a temporary folder and could be deleted by the system at any time.
        /// </summary>
        public async Task<StorageFile> CapturePhoto() {
            string fileName = GenerateNewFileName() + ".jpg";
            StorageFile file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

            await MediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);

            return file;
        }

        /// <summary>
        /// Generates unique file name based on current time and date. Returns value as string.
        /// </summary>
        private string GenerateNewFileName() {
            var dateTime = DateTime.Now.ToString("yyyy.MMM.dd HH-mm-ss");
            return $"{dateTime} - Facial Recognition";
        }
    }
}
