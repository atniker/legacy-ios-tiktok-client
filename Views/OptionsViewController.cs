using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Net; // For WebClient if using directly in options, though Atnik might abstract this
using System.Threading;

namespace TikTok.Views
{
    // Data model for a friend in the list
    public class FriendItem
    {
        public string Username { get; set; }
        public string AvatarUrl { get; set; }
        public Action OnTapAction { get; set; } // Action to perform when friend is tapped
    }

    // Data model for an option button
    public class OptionItem
    {
        public string Title { get; set; }
        public string IconName { get; set; } // e.g., "download.png", "autoscroll.png"
        public Action OnTapAction { get; set; } // Action to perform when option is tapped
    }

    // Custom UICollectionViewCell for a friend in the horizontal list
    public class FriendCell : UICollectionViewCell
    {
        private UIImageView _avatarImageView;
        private UILabel _usernameLabel;

        public static readonly NSString CellId = new NSString("FriendCell"); // Use static NSString for reuse ID

        [Export("initWithFrame:")]
        public FriendCell(RectangleF frame)
            : base(frame)
        {
            _avatarImageView = new UIImageView
            {
                Layer = { CornerRadius = 20, MasksToBounds = true }, // Half of 40x40 size for circle
                BackgroundColor = UIColor.LightGray
            };
            ContentView.AddSubview(_avatarImageView);

            _usernameLabel = new UILabel
            {
                TextAlignment = UITextAlignment.Center,
                Font = UIFont.FromName(VideoViewController.RegularFont, 12),
                TextColor = UIColor.Black,
                LineBreakMode = UILineBreakMode.TailTruncation
            };
            ContentView.AddSubview(_usernameLabel);
        }

        public void UpdateCell(FriendItem friend)
        {
            _usernameLabel.Text = friend.Username;
            _avatarImageView.Image = null; // Clear old image

            // Assuming Atnik.Tiktok.SetImage can be used for smaller images too
            // Note: CancellationTokenSource for each cell might be overkill, but good for cancellation
            var cts = new CancellationTokenSource();
            Atnik.Tiktok.SetImage((NSData data) =>
            {
                InvokeOnMainThread(() =>
                {
                    if (cts.IsCancellationRequested) return;
                    if (_avatarImageView != null)
                    {
                        _avatarImageView.Image = UIImage.LoadFromData(data);
                    }
                    if (data != null) { data.Dispose(); }
                });
            }, friend.AvatarUrl, false, cts);
            // Dispose CancellationTokenSource in PrepareForReuse or Cleanup
            // For simplicity here, it's left to GC unless explicitly managed.
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _avatarImageView.Frame = new RectangleF(
                (ContentView.Bounds.Width - 40) / 2, // Center horizontally
                5, // Top padding
                40, 40 // Size
            );
            _usernameLabel.Frame = new RectangleF(
                5, // Left padding
                _avatarImageView.Frame.Bottom + 2, // Below avatar with a small gap
                ContentView.Bounds.Width - 10, // Width with padding
                15 // Height
            );
        }

        public override void PrepareForReuse()
        {
            base.PrepareForReuse();
            _avatarImageView.Image = null; // Clear image for reuse
            _usernameLabel.Text = null;    // Clear text
        }
    }

    // Custom UICollectionViewCell for an option button
    public class OptionCell : UICollectionViewCell
    {
        private UIImageView _iconImageView;
        private UILabel _titleLabel;

        public static readonly NSString CellId = new NSString("OptionCell");

        [Export("initWithFrame:")]
        public OptionCell(RectangleF frame)
            : base(frame)
        {
            _iconImageView = new UIImageView
            {
                ContentMode = UIViewContentMode.ScaleAspectFit,
                BackgroundColor = UIColor.LightGray
            };
            ContentView.AddSubview(_iconImageView);

            _titleLabel = new UILabel
            {
                TextAlignment = UITextAlignment.Center,
                Font = UIFont.FromName(VideoViewController.RegularFont, 12),
                TextColor = UIColor.LightGray,
                LineBreakMode = UILineBreakMode.TailTruncation
            };
            ContentView.AddSubview(_titleLabel);
        }

        public void UpdateCell(OptionItem option)
        {
            _titleLabel.Text = option.Title;
            if (!string.IsNullOrEmpty(option.IconName))
            {
                _iconImageView.Image = UIImage.FromBundle(option.IconName); // Make sure these icons exist
            }
            else
            {
                _iconImageView.Image = null;
            }
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _iconImageView.Frame = new RectangleF(
                (ContentView.Bounds.Width - 30) / 2, 5, 30, 30 // Center icon, 30x30 size
            );
            _titleLabel.Frame = new RectangleF(
                5, _iconImageView.Frame.Bottom + 2, ContentView.Bounds.Width - 10, 15 // Text below icon
            );
        }

        public override void PrepareForReuse()
        {
            base.PrepareForReuse();
            _iconImageView.Image = null;
            _titleLabel.Text = null;
        }
    }

    // UICollectionViewDataSource for the friend list
    public class FriendListDataSource : UICollectionViewDataSource
    {
        private readonly List<FriendItem> _friends;

        public FriendListDataSource(List<FriendItem> friends)
        {
            _friends = friends;
        }

        public override int GetItemsCount(UICollectionView collectionView, int section)
        {
            return _friends.Count;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (FriendCell)collectionView.DequeueReusableCell(FriendCell.CellId, indexPath);
            cell.UpdateCell(_friends[indexPath.Row]);
            return cell;
        }
    }

    // UICollectionViewDelegateFlowLayout for the friend list (handles sizing and selection)
    public class FriendListDelegate : UICollectionViewDelegateFlowLayout
    {
        private readonly List<FriendItem> _friends;

        public FriendListDelegate(List<FriendItem> friends)
        {
            _friends = friends;
        }

        public override SizeF GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
        {
            // Fixed size for each friend item
            return new SizeF(80, 80); // Width 80, Height 80 (for avatar + name)
        }

        public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        {
            // Call the friend's action when tapped
            _friends[indexPath.Row].OnTapAction.Invoke();
            collectionView.DeselectItem(indexPath, true); // Deselect with animation
        }
    }

    // UICollectionViewDataSource for the options list
    public class OptionsDataSource : UICollectionViewDataSource
    {
        private readonly List<OptionItem> _options;

        public OptionsDataSource(List<OptionItem> options)
        {
            _options = options;
        }

        public override int GetItemsCount(UICollectionView collectionView, int section)
        {
            return _options.Count;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (OptionCell)collectionView.DequeueReusableCell(OptionCell.CellId, indexPath);
            cell.UpdateCell(_options[indexPath.Row]);
            return cell;
        }
    }

    // UICollectionViewDelegateFlowLayout for the options list (handles sizing and selection)
    public class OptionsDelegate : UICollectionViewDelegateFlowLayout
    {
        private readonly List<OptionItem> _options;

        public OptionsDelegate(List<OptionItem> options)
        {
            _options = options;
        }

        public override SizeF GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
        {
            // Fixed size for each option item
            return new SizeF(100, 80); // Width 100, Height 80 (for icon + title)
        }

        public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        {
            // Call the option's action when tapped
            _options[indexPath.Row].OnTapAction.Invoke();
            collectionView.DeselectItem(indexPath, true); // Deselect with animation
        }
    }


    // The main Options View Controller
    public class OptionsViewController : UIViewController
    {
        private string _videoId;
        private string _videoUrl;
        private UIView _containerView;
        private UILabel _titleLabel;
        private UIButton _closeButton;

        private UILabel _friendsLabel;
        private UILabel _optionsLabel;

        // --- NEW: Visual separators ---
        private UIView _friendSectionSeparator;
        private UIView _optionSectionSeparator;

        private UICollectionView _friendsCollectionView;
        private FriendListDataSource _friendsDataSource;
        private FriendListDelegate _friendsDelegate;
        private List<FriendItem> _friends;

        private UICollectionView _optionsCollectionView;
        private OptionsDataSource _optionsDataSource;
        private OptionsDelegate _optionsDelegate;
        private List<OptionItem> _options;

        public OptionsViewController(string videoId, string videoUrl)
        {
            _videoId = videoId;
            _videoUrl = videoUrl;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            View.BackgroundColor = UIColor.FromRGBA(0, 0, 0, 0.5f);
            var dismissTap = new UITapGestureRecognizer(OnDismissTapped);
            View.AddGestureRecognizer(dismissTap);
            dismissTap.ShouldReceiveTouch = (recognizer, touch) =>
            {
                return !_containerView.Frame.Contains(touch.LocationInView(View));
            };

            var containerHeight = View.Bounds.Height * 2 / 3;
            var containerRect = new RectangleF(0, View.Bounds.Height - containerHeight, View.Bounds.Width, containerHeight);
            _containerView = new UIView(containerRect);
            _containerView.BackgroundColor = UIColor.White;
            _containerView.Layer.CornerRadius = 16;
            _containerView.ClipsToBounds = true;
            View.AddSubview(_containerView);

            _titleLabel = new UILabel(new RectangleF(16, 8, 200, 30));
            _titleLabel.Text = "Video Options";
            _titleLabel.Font = UIFont.FromName(VideoViewController.BoldFont, 18);
            _containerView.AddSubview(_titleLabel);

            _closeButton = new UIButton(UIButtonType.System)
            {
                Frame = new RectangleF(_containerView.Bounds.Width - 40, 8, 30, 30),
                TintColor = UIColor.Gray
            };
            _closeButton.SetTitle("✖️", UIControlState.Normal);
            _closeButton.TouchUpInside += (sender, e) => DismissViewController(true, null);
            _containerView.AddSubview(_closeButton);

            // --- Layout Constants ---
            var horizontalPadding = (float)10;
            var labelWidth = (float)80; // Width for "Friends" or "Options" label
            var sectionVerticalSpacing = (float)20; // Space between sections
            var labelToCollectionGap = (float)5; // Space between label and collection view
            var separatorHeight = (float)1; // Height of the separator line

            // --- Friends Section ---
            _friendsLabel = new UILabel(new RectangleF(horizontalPadding, _titleLabel.Frame.Bottom + 10, labelWidth, 20))
            {
                Text = "Friends",
                Font = UIFont.FromName(VideoViewController.BoldFont, 14),
                TextColor = UIColor.DarkGray,
                TextAlignment = UITextAlignment.Left
            };
            _containerView.AddSubview(_friendsLabel);

            var friendsCollectionX = _friendsLabel.Frame.Right + labelToCollectionGap;
            var friendsCollectionWidth = _containerView.Bounds.Width - friendsCollectionX - horizontalPadding;
            var friendsCollectionHeight = (float)90; // Height 90 for 80px cell + padding
            var friendsCollectionY = _friendsLabel.Frame.Y - 5; // Align top of collection with label, or slightly higher

            var friendsLayout = new UICollectionViewFlowLayout
            {
                ScrollDirection = UICollectionViewScrollDirection.Horizontal,
                MinimumLineSpacing = 10,
                MinimumInteritemSpacing = 0,
                SectionInset = new UIEdgeInsets(0, 0, 0, 0) // No inset here, outer padding handled by collection view frame
            };
            _friendsCollectionView = new UICollectionView(new RectangleF(friendsCollectionX, friendsCollectionY, friendsCollectionWidth, friendsCollectionHeight), friendsLayout)
            {
                BackgroundColor = UIColor.Clear,
                ShowsHorizontalScrollIndicator = false,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth
            };
            _friendsCollectionView.RegisterClassForCell(typeof(FriendCell), FriendCell.CellId);
            _containerView.AddSubview(_friendsCollectionView);

            _friendSectionSeparator = new UIView(new RectangleF(horizontalPadding, _friendsCollectionView.Frame.Bottom + 10, _containerView.Bounds.Width - (horizontalPadding * 2), separatorHeight))
            {
                BackgroundColor = UIColor.LightGray // Light gray line
            };
            _containerView.AddSubview(_friendSectionSeparator);


            // --- Options Section ---
            _optionsLabel = new UILabel(new RectangleF(horizontalPadding, _friendSectionSeparator.Frame.Bottom + sectionVerticalSpacing, labelWidth, 20))
            {
                Text = "Options",
                Font = UIFont.FromName(VideoViewController.BoldFont, 14),
                TextColor = UIColor.DarkGray,
                TextAlignment = UITextAlignment.Left
            };
            _containerView.AddSubview(_optionsLabel);

            var optionsCollectionX = _optionsLabel.Frame.Right + labelToCollectionGap;
            var optionsCollectionWidth = _containerView.Bounds.Width - optionsCollectionX - horizontalPadding;
            var optionsCollectionHeight = (float)90; // Height 90 for 80px cell + padding
            var optionsCollectionY = _optionsLabel.Frame.Y - 5; // Align top of collection with label, or slightly higher

            var optionsLayout = new UICollectionViewFlowLayout
            {
                ScrollDirection = UICollectionViewScrollDirection.Horizontal,
                MinimumLineSpacing = 10,
                MinimumInteritemSpacing = 0,
                SectionInset = new UIEdgeInsets(0, 0, 0, 0)
            };
            _optionsCollectionView = new UICollectionView(new RectangleF(optionsCollectionX, optionsCollectionY, optionsCollectionWidth, optionsCollectionHeight), optionsLayout)
            {
                BackgroundColor = UIColor.Clear,
                ShowsHorizontalScrollIndicator = false,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth
            };
            _optionsCollectionView.RegisterClassForCell(typeof(OptionCell), OptionCell.CellId);
            _containerView.AddSubview(_optionsCollectionView);

            _optionSectionSeparator = new UIView(new RectangleF(horizontalPadding, _optionsCollectionView.Frame.Bottom + 10, _containerView.Bounds.Width - (horizontalPadding * 2), separatorHeight))
            {
                BackgroundColor = UIColor.LightGray
            };
            _containerView.AddSubview(_optionSectionSeparator);


            // Load Data
            LoadFriendsData();
            LoadOptionsData();

            // Assign Data Sources and Delegates
            _friendsDataSource = new FriendListDataSource(_friends);
            _friendsDelegate = new FriendListDelegate(_friends);
            _friendsCollectionView.DataSource = _friendsDataSource;
            _friendsCollectionView.Delegate = _friendsDelegate;

            _optionsDataSource = new OptionsDataSource(_options);
            _optionsDelegate = new OptionsDelegate(_options);
            _optionsCollectionView.DataSource = _optionsDataSource;
            _optionsCollectionView.Delegate = _optionsDelegate;
        }

        private void OnDismissTapped(UITapGestureRecognizer recognizer)
        {
            // This method will only be called if the touch was outside _containerView
            DismissViewController(true, null);
        }


        private void LoadFriendsData()
        {
            _friends = new List<FriendItem>
            {
            };
        }

        private void LoadOptionsData()
        {
            _options = new List<OptionItem>
            {
                new OptionItem
                {
                    Title = "Download",
                    IconName = "download.png", // Ensure you have this image in your project
                    OnTapAction = async () =>
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Download Video: {0}", _videoId));
                        // Example: Use Atnik.Tiktok to initiate download
                        // You'd need to adapt this to your Atnik.Tiktok download function
                        try
                        {
                            // This would be a simplified call, assuming Atnik.Tiktok handles actual download logic
                            // If Atnik.Warmboot.LoadVideos is for actual caching, you might call it directly,
                            // or create a specific download method in Atnik.Tiktok.
                            //await Atnik.Tiktok.DownloadVideoAsync(_videoId, _videoUrl); // Placeholder method
                            InvokeOnMainThread(() => {
                                var alert = new UIAlertView("Success", "Video Downloaded!", null, "OK", null);
                                alert.Show();
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Download failed: {0}", ex.Message));
                            InvokeOnMainThread(() => {
                                var alert = new UIAlertView("Error", string.Format("Failed to download: {0}", ex.Message), null, "OK", null);
                                alert.Show();
                            });
                        }
                    }
                },
                new OptionItem
                {
                    Title = "Autoscroll",
                    IconName = "autoscroll.png", // Ensure you have this image
                    OnTapAction = () =>
                    {
                        System.Diagnostics.Debug.WriteLine("Toggle Autoscroll");
                        // Example: Toggle a setting in your app
                        // Atnik.Tiktok.ToggleAutoscroll(); // Placeholder method
                        InvokeOnMainThread(() => {
                            var alert = new UIAlertView("Info", "Autoscroll Toggled", null, "OK", null);
                            alert.Show();
                        });
                    }
                },
                new OptionItem
                {
                    Title = "Share",
                    IconName = "link.png", // Ensure you have this image
                    OnTapAction = () =>
                    {
                        System.Diagnostics.Debug.WriteLine("Share Video");
                        // Implement sharing logic (e.g., UIActivityViewController)
                        InvokeOnMainThread(() => {
                            var alert = new UIAlertView("Info", "Share functionality not implemented", null, "OK", null);
                            alert.Show();
                        });
                    }
                },
                // Add more options as needed
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from button events
                if (_closeButton != null) { _closeButton.TouchUpInside -= (sender, e) => DismissViewController(true, null); }

                // Dispose CollectionViews and their components
                if (_friendsCollectionView != null)
                {
                    _friendsCollectionView.DataSource = null;
                    _friendsCollectionView.Delegate = null;
                    _friendsCollectionView.Dispose();
                    _friendsCollectionView = null;
                }
                if (_optionsCollectionView != null)
                {
                    _optionsCollectionView.DataSource = null;
                    _optionsCollectionView.Delegate = null;
                    _optionsCollectionView.Dispose();
                    _optionsCollectionView = null;
                }

                // Other UI elements are typically handled by the container/base.Dispose
                if (_containerView != null) { _containerView.Dispose(); _containerView = null; }
                if (_titleLabel != null) { _titleLabel.Dispose(); _titleLabel = null; }
                if (_closeButton != null) { _closeButton.Dispose(); _closeButton = null; }
            }
            base.Dispose(disposing);
        }
    }
}