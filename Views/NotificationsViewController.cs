using System;
using System.Collections.Generic;
using System.Drawing; // For RectangleF, SizeF
using MonoTouch.CoreAnimation; // For CALayer.CornerRadius
using MonoTouch.Foundation; // For NSString, NSIndexPath, RegisterAttribute
using MonoTouch.UIKit; // For all UI elements
using System.Threading;

namespace TikTok.Views.Notifications
{
    public class Notification
    {
        public string AvatarImageName { get; set; } // Name of image in Resources/
        public string Title { get; set; }
        public string Description { get; set; }
        public string ProfileUrl { get; set; }

        public Notification(string avatar, string title, string description, string profile_url)
        {
            AvatarImageName = avatar;
            Title = title;
            Description = description;
            ProfileUrl = profile_url;
        }
    }

    // The main View Controller that hosts the UITableView
    [Register("NotificationsViewController")]
    public class NotificationsViewController : UIViewController
    {
        private UITableView tableView;
        private List<Notification> notifications;
        private NotificationSource dataSource;
        private NotificationDelegate tableViewDelegate;
        private UIActivityIndicatorView _activityLoader;
        public CancellationTokenSource _cts;
        public static NotificationsViewController instance;

        public NotificationsViewController() : base(null, null) // Classic API way for no XIB/Bundle
        {
            Title = "Notifications";
            instance = this;
        }

        async private void Loader()
        {
            try
            {
                _cts = new CancellationTokenSource();
                notifications = await Atnik.Tiktok.GetNotifications(_cts);
            }
            catch (Exception ex)
            {
                var alert = new UIAlertView("error", "couldn't load notifications", null, "ok");
                alert.Show();
                return;
            }

            tableView = new UITableView(View.Bounds, UITableViewStyle.Plain);
            tableView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;

            tableView.RegisterClassForCellReuse(typeof(NotificationCell), NotificationCell.CellId);

            dataSource = new NotificationSource(notifications); 
            tableViewDelegate = new NotificationDelegate(notifications); 

            tableView.Source = dataSource;
            tableView.Delegate = tableViewDelegate;

            View.AddSubview(tableView);

            _activityLoader.StopAnimating();
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Title for the navigation bar
            View.BackgroundColor = UIColor.White; // Set a background color for clarity
            View.Frame = new RectangleF(0, 0, View.Frame.Width, View.Frame.Height - TabBarController.TabBar.Frame.Height);

            _activityLoader = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Gray)
            {
                Frame = new RectangleF(View.Bounds.Width / 2 - 30, View.Bounds.Height / 2 - 30, 60, 60),
                BackgroundColor = UIColor.Clear,
                HidesWhenStopped = true
            };
            Add(_activityLoader);

            _activityLoader.StartAnimating();
        }

        public Action GetProfileOpener(string author)
        {
            return () =>
            {
                var profileController = new Profile.ProfileViewController(author);
                NavigationController.PushViewController(profileController, true);
            };
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            Loader();
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidAppear(animated);
            Predispose();
        }

        public void Predispose()
        {
            if (tableView != null)
            {
                tableView.Source = null;
                tableView.Delegate = null;
            }

            // Explicitly dispose of subviews if they are custom and manage resources
            // For UITableView, its internal disposal usually handles its cells, but good practice.
            if (tableView != null)
            {
                tableView.RemoveFromSuperview();
                tableView.Dispose(); // Dispose the table view itself
                tableView = null;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            // Nullify references to allow GC to collect these objects
            dataSource = null;
            tableViewDelegate = null;
            notifications = null; // Clear th
        }

        // Dispose method for the UIViewController to prevent memory leaks
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Predispose();
            }
            base.Dispose(disposing);
        }

        // =========================================================================
        // Nested Classes for Data Model, Cell, Source, and Delegate
        // =========================================================================

        // Data Model for a single notification

        // Custom UITableViewCell for displaying a notification
        internal class NotificationCell : UITableViewCell
        {
            public static readonly NSString CellId = new NSString("NotificationCell");

            private UIImageView avatarImageView;
            private UILabel titleLabel;
            private UILabel descriptionLabel;

            // Constants for layout
            private const float AvatarSize = 50.0f;
            private const float HorizontalPadding = 10.0f;
            private const float VerticalPadding = 10.0f;
            private const float InnerPadding = 4.0f; // Between avatar and text, or title and description

            // Constructor for programmatic creation or when dequeued if not registered
            [Export("initWithStyle:reuseIdentifier:")]
            public NotificationCell(UITableViewCellStyle style, string reuseIdentifier) : base(style, reuseIdentifier)
            {
                InitializeViews();
            }

            // The constructor that is called when using RegisterClassForCellReuse
            public NotificationCell(IntPtr handle) : base(handle)
            {
                InitializeViews();
            }

            private void InitializeViews()
            {
                // Avatar Image View
                avatarImageView = new UIImageView
                {
                    BackgroundColor = UIColor.LightGray, // Placeholder color
                    ContentMode = UIViewContentMode.ScaleAspectFill,
                    ClipsToBounds = true // Crucial for rounded corners
                };
                avatarImageView.Layer.CornerRadius = AvatarSize / 2.0f; // Initial corner radius
                ContentView.AddSubview(avatarImageView);

                // Title Label
                titleLabel = new UILabel
                {
                    Font = UIFont.BoldSystemFontOfSize(14f),
                    TextColor = UIColor.Black,
                    Lines = 1, // Single line title
                    LineBreakMode = UILineBreakMode.TailTruncation
                };
                ContentView.AddSubview(titleLabel);

                // Description Label
                descriptionLabel = new UILabel
                {
                    Font = UIFont.SystemFontOfSize(12.0f),
                    TextColor = UIColor.DarkGray,
                    Lines = 0, // Allows multiple lines
                    LineBreakMode = UILineBreakMode.WordWrap
                };
                ContentView.AddSubview(descriptionLabel);
            }

            // Method to update cell content with a Notification object
            public void UpdateCell(Notification notification)
            {
                // Set avatar
                if (!string.IsNullOrEmpty(notification.AvatarImageName))
                {
                    Atnik.Tiktok.SetImage((NSData data) =>
                    {
                        avatarImageView.Image = UIImage.LoadFromData(data);
                    }, notification.AvatarImageName, cancellationToken: instance._cts);
                    // Ensure rounding is applied in case cell was reused with different properties
                    // or if the image itself caused issues.
                    avatarImageView.Layer.CornerRadius = AvatarSize / 2.0f; 
                    avatarImageView.ClipsToBounds = true;
                }
                else
                {
                    avatarImageView.Image = UIImage.FromBundle("avatar-placeholder.jpg"); 
                }

                // Set text
                titleLabel.Text = notification.Title;
                descriptionLabel.Text = notification.Description;

                // Mark for layout update (important for text resizing)
                SetNeedsLayout();
            }

            public override void LayoutSubviews()
            {
                base.LayoutSubviews();

                // Calculate available width for text
                float textStartX = HorizontalPadding + AvatarSize + InnerPadding;
                float textMaxWidth = ContentView.Bounds.Width - textStartX - HorizontalPadding;

                // Position Avatar
                avatarImageView.Frame = new RectangleF(
                    HorizontalPadding, 
                    (ContentView.Bounds.Height - AvatarSize) / 2.0f, // Center vertically
                    AvatarSize, 
                    AvatarSize
                );
                // Re-apply corner radius in LayoutSubviews to ensure it's correct after layout
                avatarImageView.Layer.CornerRadius = AvatarSize / 2.0f; 

                // Calculate title label height and position
                SizeF titleSize = new NSString(titleLabel.Text).StringSize(
                    titleLabel.Font,
                    textMaxWidth,
                    UILineBreakMode.TailTruncation
                );
                titleLabel.Frame = new RectangleF(
                    textStartX,
                    VerticalPadding,
                    textMaxWidth,
                    titleSize.Height
                );

                // Calculate description label height and position
                SizeF descriptionSize = new NSString(descriptionLabel.Text).StringSize(
                    descriptionLabel.Font,
                    textMaxWidth,
                    UILineBreakMode.WordWrap
                );
                descriptionLabel.Frame = new RectangleF(
                    textStartX,
                    titleLabel.Frame.Bottom + InnerPadding, // Below title
                    textMaxWidth,
                    descriptionSize.Height
                );
            }

            // Static method to calculate the required height for a cell based on its content
            public static float GetCellHeight(Notification notification, float tableViewWidth)
            {
                // Constants must match those used in the cell's LayoutSubviews
                const float cellAvatarSize = 60.0f;
                const float cellHorizontalPadding = 15.0f;
                const float cellVerticalPadding = 10.0f;
                const float cellInnerPadding = 8.0f;

                // Calculate available width for text, same logic as in LayoutSubviews
                float textStartX = cellHorizontalPadding + cellAvatarSize + cellInnerPadding;
                float textMaxWidth = tableViewWidth - textStartX - cellHorizontalPadding;

                // Calculate title height
                SizeF titleSize = new NSString(notification.Title).StringSize(
                    UIFont.BoldSystemFontOfSize(17.0f), // Must match font in cell
                    textMaxWidth,
                    UILineBreakMode.TailTruncation
                );

                // Calculate description height
                SizeF descriptionSize = new NSString(notification.Description).StringSize(
                    UIFont.SystemFontOfSize(14.0f), // Must match font in cell
                    textMaxWidth,
                    UILineBreakMode.WordWrap
                );
                
                // Total height needed for text content
                float textContentHeight = titleSize.Height + cellInnerPadding + descriptionSize.Height;

                // Take the max of avatar height or text content height, plus vertical padding
                return Math.Max(cellAvatarSize, textContentHeight) + (cellVerticalPadding * 2.0f);
            }

            // Dispose method to release resources and prevent memory leaks
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Remove subviews from content view before they are disposed by the base.
                    // This isn't strictly necessary for most simple cases where subviews are
                    // part of the view hierarchy, but it's good practice for complex custom views.
                    avatarImageView.RemoveFromSuperview();
                    titleLabel.RemoveFromSuperview();
                    descriptionLabel.RemoveFromSuperview();

                    // Nullify references to allow GC
                    avatarImageView = null;
                    titleLabel = null;
                    descriptionLabel = null;
                }
                base.Dispose(disposing);
            }
        }

        // UITableViewSource handles providing data to the table view
        internal class NotificationSource : UITableViewSource
        {
            private List<Notification> notifications;

            public NotificationSource(List<Notification> notifications)
            {
                this.notifications = notifications;
            }

            public override int RowsInSection(UITableView tableView, int section)
            {
                return notifications.Count;
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                NotificationCell cell = (NotificationCell)tableView.DequeueReusableCell(NotificationCell.CellId);
                if (cell == null)
                {
                    // This case should ideally not happen if RegisterClassForCellReuse is used correctly,
                    // but it's good defensive programming for older Monotouch versions.
                    cell = new NotificationCell(UITableViewCellStyle.Default, NotificationCell.CellId);
                }

                // Update the cell with new data from the list
                cell.UpdateCell(notifications[indexPath.Row]);
                return cell;
            }
        }

        // UITableViewDelegate handles table view behavior, like cell height and selection
        internal class NotificationDelegate : UITableViewDelegate
        {
            private List<Notification> notifications;

            public NotificationDelegate(List<Notification> notifications)
            {
                this.notifications = notifications;
            }

            public override float GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
            {
                // It's crucial to pass the current table view width for accurate height calculation
                // as the text wraps based on the available width.
                return NotificationCell.GetCellHeight(notifications[indexPath.Row], tableView.Bounds.Width);
            }

            // Handle row selection (optional)
            public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                Console.WriteLine("Notification selected: {notifications[indexPath.Row].Title}");
                tableView.DeselectRow(indexPath, true); // Deselect the row with animation

                string author = notifications[indexPath.Row].ProfileUrl;

                if (author == null)
                {
                    return;
                }

                instance.GetProfileOpener(author).Invoke();
            }
        }
    }
}