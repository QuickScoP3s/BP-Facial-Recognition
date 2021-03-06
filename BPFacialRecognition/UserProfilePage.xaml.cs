﻿using BPFacialRecognition.Helpers;
using BPFacialRecognition.Objects;

using System;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BPFacialRecognition {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserProfilePage : Page {

        private CameraHelper camera;

        private Visitor currentUser;
        private Image[] userIDImages;
        private double idImageMaxWidth = 0;

        private bool CanPreview => (this.camera != null && this.camera.Initialized) && this.camera.MediaCapture != null;
        private bool IsPreviewing => WebcamFeed.Source != null;

        public UserProfilePage() {
            this.InitializeComponent();
        }

        /// <summary>
        /// Triggered every time the page is navigated to.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            try {
                // Catches the passed UserProfilePage parameters
                UserProfileObject userProfileParameters = e.Parameter as UserProfileObject;

                // Sets current user as the passed through Visitor object
                currentUser = userProfileParameters.Visitor;

                // Sets the VisitorNameBlock as the current user's name
                VisitorNameBlock.Text = currentUser.Name;

                // Sets the local WebcamHelper as the passed through intialized one
                camera = userProfileParameters.WebcamHelper;
            }
            catch {
                // Something went wrong... It's likely the page was navigated to without a Visitor parameter. Navigate back to MainPage
                Frame.GoBack();
            }
        }

        private void PhotoGrid_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e) {
            // Populate photo grid with visitor ID photos:
            PopulatePhotoGrid();
        }

        private async void WebcamFeed_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e) {
            if (!IsPreviewing) {
                try {
                    WebcamFeed.Source = this.camera.MediaCapture;
                    await this.camera.StartCameraPreview();
                }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Populates PhotoGrid on UserProfilePage.xaml with photos in ImageFolder of passed through visitor object
        /// </summary>
        private async void PopulatePhotoGrid() {
            // Sets max width to allow 6 photos to sit in one row
            idImageMaxWidth = PhotoGrid.ActualWidth / 6 - 10;

            var filesInFolder = await currentUser.ImageFolder.GetFilesAsync();

            userIDImages = new Image[filesInFolder.Count];

            for (int i = 0; i < filesInFolder.Count; i++) {
                var photoStream = await filesInFolder[i].OpenAsync(FileAccessMode.Read);
                BitmapImage idImage = new BitmapImage();
                await idImage.SetSourceAsync(photoStream);

                Image idImageControl = new Image();
                idImageControl.Source = idImage;
                idImageControl.MaxWidth = idImageMaxWidth;

                userIDImages[i] = idImageControl;
            }

            PhotoGrid.ItemsSource = userIDImages;
        }

        /// <summary>
        /// Triggered when the user clicks the add photo button located in the app bar
        /// </summary>
        private async void AddButton_Tapped(object sender, TappedRoutedEventArgs e) {
            // Captures photo from current webcam stream
            StorageFile imageFile = await camera.CapturePhoto();

            // Moves the captured file to the current user's ID image folder
            await imageFile.MoveAsync(currentUser.ImageFolder);
            PopulatePhotoGrid();

            FaceAPIHelper.AddImageToWhitelist(imageFile, currentUser.Name);
        }

        /// <summary>
        /// Triggered when the user clicks the delete user button located in the app bar
        /// </summary>
        private async void DeleteButton_Tapped(object sender, TappedRoutedEventArgs e) {
            await currentUser.ImageFolder.DeleteAsync();
            FaceAPIHelper.RemoveUserFromWhitelist(currentUser.Name);

            await CleanupCameraAsync();

            // Navigate to MainPage
            Frame.GoBack();
        }

        private async void BackButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e) {
            await CleanupCameraAsync();

            // Navigate to MainPage
            Frame.GoBack();
        }

        /// <summary>
        /// Cleanup resource access to the camera
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync() {
            if (CanPreview) {
                if (IsPreviewing)
                    await this.camera.StopCameraPreview();

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    WebcamFeed.Source = null;
                });
            }

        }
    }
}
