using System;
using MonoTouch.UIKit;
using System.Drawing;
using System.Threading.Tasks;
using MonoTouch.Foundation;
using System.IO; // For file operations
using System.Collections.Generic; // For Dictionary if using JSON parser

namespace TikTok.Views
{
    public class WelcomeViewController : UIViewController
    {
        UIColor transparency;

        // UI elements for the initial welcome screen
        UILabel welcomeLabel;
        UILabel subWelcomeLabel;
        UIButton nextButton;

        // --- NEW: UI elements for the JSON file selection screen ---
        UILabel filePromptLabel;
        UILabel fileInstructionLabel; // To tell user where to place the file
        UIButton selectFileButton; // Button to trigger reading the file

        // UI elements for the "contacting" state
        UIActivityIndicatorView activityIndicator;
        UILabel contactingLabel;

        // Constants for layout
        const float buttonWidth = 200;
        const float buttonHeight = 50;
        const float padding = 10;

        // --- NEW: Expected JSON file name ---
        const string MsTokenFileName = "ttcookies.json";

        public static bool failed;

        public WelcomeViewController()
        {
            Atnik.Warmboot.Cleanup();
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            transparency = UIColor.Clear;

            View.Frame = UIScreen.MainScreen.Bounds;
            View.BackgroundColor = UIColor.ScrollViewTexturedBackgroundColor;
            View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            // --- Setup Initial Welcome Screen Elements ---
            welcomeLabel = new UILabel(new RectangleF(
                padding, View.Frame.Height / 2 - 60, View.Frame.Width - padding * 2, 30));
            welcomeLabel.Text = "Welcome to TikTok";
            welcomeLabel.Font = UIFont.BoldSystemFontOfSize(25);
            welcomeLabel.TextAlignment = UITextAlignment.Center;
            welcomeLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            welcomeLabel.BackgroundColor = transparency;
            welcomeLabel.TextColor = UIColor.White;
            View.AddSubview(welcomeLabel);

            subWelcomeLabel = new UILabel(new RectangleF(
                padding, welcomeLabel.Frame.Bottom, View.Frame.Width - padding * 2, 20));
            subWelcomeLabel.Text = "An iOS 3 client made by Atnik (early alpha 1)";
            subWelcomeLabel.Font = UIFont.SystemFontOfSize(15);
            subWelcomeLabel.TextAlignment = UITextAlignment.Center;
            subWelcomeLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            subWelcomeLabel.BackgroundColor = transparency;
            subWelcomeLabel.TextColor = UIColor.White;
            View.AddSubview(subWelcomeLabel);

            nextButton = UIButton.FromType(UIButtonType.RoundedRect);
            nextButton.SetTitle("Next", UIControlState.Normal);
            nextButton.Frame = new RectangleF(
                View.Frame.Width / 2 - buttonWidth / 2, View.Frame.Height / 2 + 50, buttonWidth, buttonHeight);
            nextButton.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            nextButton.TouchUpInside += HandleNextButtonTouchUpInside;
            View.AddSubview(nextButton);


            // --- NEW: Setup JSON File Selection Screen Elements (Initially off-screen to the right) ---

            // File Prompt Label (e.g., "Select your MsToken file")
            filePromptLabel = new UILabel(new RectangleF(
                View.Frame.Width + padding, // Start off-screen
                View.Frame.Height / 2 - 100,
                View.Frame.Width - padding * 2,
                30));
            filePromptLabel.Text = "Please select your Cookies file:";
            filePromptLabel.Font = UIFont.BoldSystemFontOfSize(18);
            filePromptLabel.TextAlignment = UITextAlignment.Center;
            filePromptLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            filePromptLabel.BackgroundColor = transparency;
            filePromptLabel.TextColor = UIColor.White;
            View.AddSubview(filePromptLabel);

            // File Instruction Label (e.g., "Place mstoken.json in iTunes File Sharing")
            fileInstructionLabel = new UILabel(new RectangleF(
                View.Frame.Width + padding, // Start off-screen
                filePromptLabel.Frame.Bottom + padding,
                View.Frame.Width - padding * 2,
                80)); // Increased height for multiple lines
            fileInstructionLabel.Text = string.Format("1. Connect device to iTunes.\n2. Select your app in File Sharing.\n3. Place '{0}' in its Documents.", MsTokenFileName);
            fileInstructionLabel.Font = UIFont.SystemFontOfSize(14);
            fileInstructionLabel.TextAlignment = UITextAlignment.Center;
            fileInstructionLabel.LineBreakMode = UILineBreakMode.WordWrap; // Allow wrapping
            fileInstructionLabel.Lines = 0; // Unlimited lines
            fileInstructionLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            fileInstructionLabel.BackgroundColor = transparency;
            fileInstructionLabel.TextColor = UIColor.White;
            View.AddSubview(fileInstructionLabel);

            // Select File Button
            selectFileButton = UIButton.FromType(UIButtonType.RoundedRect);
            selectFileButton.SetTitle("Select Cookies File", UIControlState.Normal);
            selectFileButton.Frame = new RectangleF(
                View.Frame.Width + View.Frame.Width / 2 - buttonWidth / 2, // Start off-screen
                View.Frame.Height / 2 + 50,
                buttonWidth,
                buttonHeight);
            selectFileButton.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            selectFileButton.TouchUpInside += HandleSelectFileButtonTouchUpInside;
            View.AddSubview(selectFileButton);


            // --- Setup "Contacting" Elements (Initially hidden) ---
            activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Gray);
            activityIndicator.Frame = new RectangleF(
                View.Frame.Width / 2 - activityIndicator.Frame.Width / 2, View.Frame.Height / 2,
                activityIndicator.Frame.Width, activityIndicator.Frame.Height);
            activityIndicator.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin | UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleRightMargin;
            activityIndicator.Hidden = true;
            View.AddSubview(activityIndicator);

            contactingLabel = new UILabel(new RectangleF(
                padding, activityIndicator.Frame.Bottom + padding, View.Frame.Width - padding * 2, 20));
            contactingLabel.Text = "contacting";
            contactingLabel.Font = UIFont.SystemFontOfSize(14);
            contactingLabel.TextAlignment = UITextAlignment.Center;
            contactingLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            contactingLabel.Hidden = true;
            contactingLabel.BackgroundColor = transparency;
            contactingLabel.TextColor = UIColor.White;
            View.AddSubview(contactingLabel);
        }

        async private void Auth()
        {
            var alert = new UIAlertView("Error", "connection failed, please redo the setup", null, "ok");
            alert.Clicked += (object s, UIButtonEventArgs e) =>
            {
                AppDelegate.instance.RestartApplicationUI();
            };

            try
            {
                contactingLabel.Text = "logging in";

                try
                {
                    await Atnik.Tiktok.AcquireSession();
                }
                catch (System.Net.WebException ex)
                {
                    var r = ex.Response as System.Net.HttpWebResponse;

                    if (r != null)
                    {
                        if (r.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                        {
                            alert.Message = "the server couldn't login into your TikTok account";
                        }
                    }

                    alert.Show();
                    return;
                }

                contactingLabel.Text = "loading videos";

                var initFyp = await Atnik.Tiktok.GetForYouPage();

                if (initFyp.Count < 1)
                {
                    alert.Message = "the server didn't return any videos indicating that it's either broken or under maintenance";
                    alert.Show();

                    return;
                }

                var videoViewController = new VideoViewController(initFyp);
                var profileViewController = new Views.Profile.ProfileViewController(Atnik.Tiktok.myProfile);

                AppDelegate.instance.SetOnLoad(videoViewController, new Notifications.NotificationsViewController(), new MessagesViewController(""), profileViewController);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                alert.Show();
            }
        }

        // Event handler for the "Next" button (transitions to file selection)
        void HandleNextButtonTouchUpInside(object sender, EventArgs e)
        {
            // Disable the next button to prevent multiple taps
            nextButton.Enabled = false;

            // Animate the slide from right to left
            UIView.Animate(
                0.5, // duration in seconds
                () =>
                {
                    // Slide the welcome elements off-screen to the left
                    welcomeLabel.Frame = new RectangleF(
                        -View.Frame.Width - padding, welcomeLabel.Frame.Y, welcomeLabel.Frame.Width, welcomeLabel.Frame.Height);
                    subWelcomeLabel.Frame = new RectangleF(
                        -View.Frame.Width - padding, subWelcomeLabel.Frame.Y, subWelcomeLabel.Frame.Width, subWelcomeLabel.Frame.Height);
                    nextButton.Frame = new RectangleF(
                        -View.Frame.Width - padding, nextButton.Frame.Y, nextButton.Frame.Width, nextButton.Frame.Height);

                    // Slide the file selection elements into view
                    filePromptLabel.Frame = new RectangleF(
                        padding, filePromptLabel.Frame.Y, filePromptLabel.Frame.Width, filePromptLabel.Frame.Height);
                    fileInstructionLabel.Frame = new RectangleF(
                        padding, fileInstructionLabel.Frame.Y, fileInstructionLabel.Frame.Width, fileInstructionLabel.Frame.Height);
                    selectFileButton.Frame = new RectangleF(
                        View.Frame.Width / 2 - buttonWidth / 2, selectFileButton.Frame.Y, buttonWidth, buttonHeight);
                },
                () =>
                {
                    // Animation completion block (optional)
                }
            );
        }

        // --- NEW: Event handler for the "Select MSToken File" button ---
        void HandleSelectFileButtonTouchUpInside(object sender, EventArgs e)
        {
            // Disable the button to prevent multiple taps
            selectFileButton.Enabled = false;

            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string filePath = Path.Combine(documentsPath, MsTokenFileName);

            if (File.Exists(filePath))
            {
                // Attempt to read and parse the file
                try
                {
                    string jsonContent = File.ReadAllText(filePath);

                    // --- Placeholder for JSON parsing ---
                    // You'll need to use your actual JSON parsing library here.
                    // For example, if you're using MiniJSON:
                    // var parsedJson = MiniJSON.Json.Deserialize(jsonContent) as Dictionary<string, object>;

                    // --- Mock parsing for demonstration if MiniJSON not easily available/working ---
                    string msToken = ExtractMsTokenFromJson(jsonContent);

                    if (!string.IsNullOrEmpty(msToken))
                    {
                        Atnik.Tiktok.MsToken = msToken; // Set the MsToken in your Atnik.Tiktok class
                        System.Diagnostics.Debug.WriteLine(string.Format("MsToken loaded from file: {0}", msToken));

                        // Proceed to contacting server
                        ProceedToContactingServer();
                    }
                    else
                    {
                        ShowErrorAlert("Invalid File Content", string.Format("'{0}' does not contain a valid MsToken. Please ensure it's in the format {{\"{1}\": \"YOUR_TOKEN\"}}.", MsTokenFileName, "MsToken"));
                        selectFileButton.Enabled = true; // Re-enable button on failure
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Error reading or parsing MSToken file: {0}", ex.Message));
                    ShowErrorAlert("File Read Error", string.Format("Could not read or parse '{0}': {1}", MsTokenFileName, ex.Message));
                    selectFileButton.Enabled = true; // Re-enable button on failure
                }
            }
            else
            {
                ShowErrorAlert("File Not Found", string.Format("'{0}' not found in app's Documents folder. Full path:\n{1}", MsTokenFileName, documentsPath));
                selectFileButton.Enabled = true; // Re-enable button on failure
            }
        }

        // Helper method to transition to "contacting server" state
        private void ProceedToContactingServer()
        {
            // Hide the file selection elements
            filePromptLabel.Hidden = true;
            fileInstructionLabel.Hidden = true;
            selectFileButton.Hidden = true;

            // Show the activity indicator and "contacting" label
            activityIndicator.Hidden = false;
            activityIndicator.StartAnimating();
            contactingLabel.Hidden = false;

            Auth();
        }

        // Helper method for showing error alerts
        private void ShowErrorAlert(string title, string message)
        {
            var alert = new UIAlertView(title, message, null, "OK", null);
            alert.Show();
        }

        // --- VERY BASIC MsToken extraction for older .NET if a JSON parser isn't working ---
        // Replace this with a proper JSON deserializer if possible.
        private string ExtractMsTokenFromJson(string jsonContent)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonContent));
        }
    }
}