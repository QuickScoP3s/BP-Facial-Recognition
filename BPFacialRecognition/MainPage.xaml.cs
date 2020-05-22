using BPFacialRecognition.FacialRecognition;
using BPFacialRecognition.Helpers;
using BPFacialRecognition.Objects;

using Microsoft.ProjectOxford.Face;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BPFacialRecognition {
    public sealed partial class MainPage : Page {
        // Webcam Related Variables:
        private WebcamHelper webcam;

        // Oxford Related Variables:
        private bool initializedFaceApi = false;

        // Whitelist Related Variables:
        private List<Visitor> whitelistedVisitors = new List<Visitor>();
        private StorageFolder whitelistFolder;
        private bool currentlyUpdatingWhitelist;

        private bool doorbellJustPressed = false;

        // GUI Related Variables:
        private double visitorIDPhotoGridMaxWidth = 0;

        /// <summary>
        /// Called when the page is first navigated to.
        /// </summary>
        public MainPage() {
            InitializeComponent();

            // Causes this page to save its state when navigating to other pages
            this.NavigationCacheMode = NavigationCacheMode.Enabled;

            if (this.initializedFaceApi == false) {
                InitializeFaceApi();
            }

            // If user has set the DisableLiveCameraFeed within Constants.cs to true, disable the feed:
            if (GeneralConstants.DisableLiveCameraFeed) {
                LiveFeedPanel.Visibility = Visibility.Collapsed;
                DisabledFeedGrid.Visibility = Visibility.Visible;
            }
            else {
                LiveFeedPanel.Visibility = Visibility.Visible;
                DisabledFeedGrid.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Triggered every time the page is navigated to.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (this.initializedFaceApi) {
                UpdateWhitelistedVisitors();
            }
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes Oxford facial recognition.
        /// </summary>
        public async void InitializeFaceApi() {
            this.initializedFaceApi = await FaceAPIHelper.Initialize();

            // Populates UI grid with whitelisted visitors
            UpdateWhitelistedVisitors();
        }

        /// <summary>
        /// Triggered when webcam feed loads both for the first time and every time page is navigated to.
        /// If no WebcamHelper has been created, it creates one. Otherwise, simply restarts webcam preview feed on page.
        /// </summary>
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e) {
            if (this.webcam == null || !this.webcam.IsInitialized()) {
                // Initialize Webcam Helper
                this.webcam = new WebcamHelper();
                await this.webcam.InitializeCameraAsync();

                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = this.webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null) {
                    // Start the live feed
                    await this.webcam.StartCameraPreview();
                }
            }
            else if (this.webcam.IsInitialized()) {
                WebcamFeed.Source = this.webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null) {
                    await this.webcam.StartCameraPreview();
                }
            }
        }

        /// <summary>
        /// Triggered when the whitelisted users grid is loaded. Sets the size of each photo within the grid.
        /// </summary>
        private void WhitelistedUsersGrid_Loaded(object sender, RoutedEventArgs e) {
            this.visitorIDPhotoGridMaxWidth = (WhitelistedUsersGrid.ActualWidth / 3) - 10;
        }

        /// <summary>
        /// Triggered when user presses virtual doorbell app bar button
        /// </summary>
        private async void DoorbellButton_Click(object sender, RoutedEventArgs e) {
            if (!this.doorbellJustPressed) {
                this.doorbellJustPressed = true;
                await DoorbellPressed();
            }
        }

        /// <summary>
        /// Called when user hits physical or vitual doorbell buttons. Captures photo of current webcam view and sends it to Oxford for facial recognition processing.
        /// </summary>
        private async Task DoorbellPressed() {
            // Display analysing visitors grid to inform user that doorbell press was registered
            AnalysingVisitorGrid.Visibility = Visibility.Visible;

            // List to store visitors recognized by Oxford Face API
            // Count will be greater than 0 if there is an authorized visitor at the door
            List<string> recognizedVisitors = new List<string>();

            // Confirms that webcam has been properly initialized and oxford is ready to go
            if (this.webcam.IsInitialized() && this.initializedFaceApi) {
                // Stores current frame from webcam feed in a temporary folder
                StorageFile image = await this.webcam.CapturePhoto();

                try {
                    // Oxford determines whether or not the visitor is on the Whitelist and returns true if so
                    recognizedVisitors = await FaceAPIHelper.IsFaceInWhitelist(image);
                }
                catch (FaceRecognitionException fe) {
                    switch (fe.ExceptionType) {
                        // Fails and catches as a FaceRecognitionException if no face is detected in the image
                        case FaceRecognitionExceptionType.NoFaceDetected:
                            Debug.WriteLine("WARNING: No face detected in this image.");
                            break;
                    }
                }
                catch (FaceAPIException faceAPIEx) {
                    Debug.WriteLine("FaceAPIException in IsFaceInWhitelist(): " + faceAPIEx.ErrorMessage);
                }
                catch {
                    // General error. This can happen if there are no visitors authorized in the whitelist
                    Debug.WriteLine("WARNING: Oxford just threw a general expception.");
                }

                if (recognizedVisitors.Count > 0) {
                    // If everything went well and a visitor was recognized, unlock the door:
                    UnlockDoor(recognizedVisitors[0]);
                }
                else {
                    MessageDialog diag = new MessageDialog("I'm sorry, you were not recognized...", "Do I know you?");
                    await diag.ShowAsync();
                }
            }
            else {
                if (!this.webcam.IsInitialized())
                    Debug.WriteLine("Unable to analyze visitor at door as the camera failed to initlialize properly.");

                if (!this.initializedFaceApi)
                    Debug.WriteLine("Unable to analyze visitor at door as Oxford Facial Recogntion is still initializing.");
                
            }

            this.doorbellJustPressed = false;
            AnalysingVisitorGrid.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Unlocks door and greets visitor
        /// </summary>
        private async void UnlockDoor(string visitorName) {
            MessageDialog diag = new MessageDialog($"Welcome {visitorName}", "Welcome!");
            await diag.ShowAsync();
        }

        /// <summary>
        /// Called when user hits vitual add user button. Navigates to NewUserPage page.
        /// </summary>
        private async void NewUserButton_Click(object sender, RoutedEventArgs e) {
            // Stops camera preview on this page, so that it can be started on NewUserPage
            await this.webcam.StopCameraPreview();

            //Navigates to NewUserPage, passing through initialized WebcamHelper object
            this.Frame.Navigate(typeof(NewUserPage), this.webcam);
        }

        /// <summary>
        /// Updates internal list of of whitelisted visitors (whitelistedVisitors) and the visible UI grid
        /// </summary>
        private async void UpdateWhitelistedVisitors() {
            // If the whitelist isn't already being updated, update the whitelist
            if (!this.currentlyUpdatingWhitelist) {
                this.currentlyUpdatingWhitelist = true;
                await UpdateWhitelistedVisitorsList();
                UpdateWhitelistedVisitorsGrid();
                this.currentlyUpdatingWhitelist = false;
            }
        }

        /// <summary>
        /// Updates the list of Visitor objects with all whitelisted visitors stored on disk
        /// </summary>
        private async Task UpdateWhitelistedVisitorsList() {
            // Clears whitelist
            this.whitelistedVisitors.Clear();

            // If the whitelistFolder has not been opened, open it
            if (this.whitelistFolder == null) {
                var picturesFolder = ApplicationData.Current.LocalCacheFolder;
                this.whitelistFolder = await picturesFolder.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populates subFolders list with all sub folders within the whitelist folders.
            // Each of these sub folders represents the Id photos for a single visitor.
            var subFolders = await this.whitelistFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in subFolders) {
                string visitorName = folder.Name;
                var filesInFolder = await folder.GetFilesAsync();

                var photoStream = await filesInFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage visitorImage = new BitmapImage();
                await visitorImage.SetSourceAsync(photoStream);

                Visitor whitelistedVisitor = new Visitor(visitorName, folder, visitorImage, this.visitorIDPhotoGridMaxWidth);

                this.whitelistedVisitors.Add(whitelistedVisitor);
            }
        }

        /// <summary>
        /// Updates UserInterface list of whitelisted users from the list of Visitor objects (WhitelistedVisitors)
        /// </summary>
        private void UpdateWhitelistedVisitorsGrid() {
            // Reset source to empty list
            WhitelistedUsersGrid.ItemsSource = new List<Visitor>();
            // Set source of WhitelistedUsersGrid to the whitelistedVisitors list
            WhitelistedUsersGrid.ItemsSource = this.whitelistedVisitors;

            // Hide Oxford loading ring
            OxfordLoadingRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Triggered when the user selects a visitor in the WhitelistedUsersGrid 
        /// </summary>
        private void WhitelistedUsersGrid_ItemClick(object sender, ItemClickEventArgs e) {
            // Navigate to UserProfilePage, passing through the selected Visitor object and the initialized WebcamHelper as a parameter
            this.Frame.Navigate(typeof(UserProfilePage), new UserProfileObject(e.ClickedItem as Visitor, this.webcam));
        }

        /// <summary>
        /// Triggered when the user selects the Shutdown button in the app bar. Closes app.
        /// </summary>
        private void ShutdownButton_Click(object sender, RoutedEventArgs e) {
            // Exit app
            Application.Current.Exit();
        }
    }
}
