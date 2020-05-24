using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace BPFacialRecognition.Helpers {
    /// <summary>
    /// Interacts with an attached camera. Allows one to easily access live webcam feed and capture a photo.
    /// </summary>
    public class CameraHelper {

        public MediaCapture MediaCapture { get; private set; }

        public bool Initialized { get; private set; } = false;

        private FaceDetector faceDetector;

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

                faceDetector = await FaceDetector.CreateAsync();

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
            var bitmap = await CaptureBitmapsAsync();
            var cropRegion = await CreateCropRegion(bitmap.software);

            var cropped = bitmap.writeable.Crop(cropRegion);

            string fileName = GenerateNewFileName() + ".jpg";
            StorageFile file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite)) {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

                Stream pixelStream = cropped.PixelBuffer.AsStream();
                byte[] pixels = new byte[pixelStream.Length];
                await pixelStream.ReadAsync(pixels, 0, pixels.Length);

                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)cropped.PixelWidth, (uint)cropped.PixelHeight,
                    96.0,
                    96.0,
                    pixels);

                await encoder.FlushAsync();
            }

            return file;
        }

        private async Task<(SoftwareBitmap software, WriteableBitmap writeable)> CaptureBitmapsAsync() {
            using var ras = new InMemoryRandomAccessStream();

            var encoding = ImageEncodingProperties.CreateJpeg();
            await MediaCapture.CapturePhotoToStreamAsync(encoding, ras);

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(ras);
            SoftwareBitmap sw = await decoder.GetSoftwareBitmapAsync();

            var writeable = new WriteableBitmap(sw.PixelWidth, sw.PixelHeight);
            sw.CopyToBuffer(writeable.PixelBuffer);

            return (sw, writeable);
        }

        private async Task<Rect> CreateCropRegion(SoftwareBitmap bitmap) {
            const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Gray8;
            if (!FaceDetector.IsBitmapPixelFormatSupported(InputPixelFormat))
                return new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);

            using var detectorInput = SoftwareBitmap.Convert(bitmap, InputPixelFormat);

            var faces = await faceDetector.DetectFacesAsync(detectorInput);
            var first = faces.FirstOrDefault();

            if (first == null)
                return new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);

            var faceBox = first.FaceBox;
            int margin = 150;

            int x = Math.Max(0, (int)faceBox.X - margin);
            int y = Math.Max(0, (int)faceBox.Y - margin);

            int width = Math.Min(bitmap.PixelWidth - x, (int) faceBox.Width + (margin * 2));
            int height = Math.Min(bitmap.PixelHeight - y, (int) faceBox.Height + (margin * 2));

            return new Rect(x, y, width, height);
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
