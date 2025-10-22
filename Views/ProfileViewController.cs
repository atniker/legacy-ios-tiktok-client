using System;
using System.Drawing; // Import for RectangleF (FRectangle equivalent)
using System.Collections.Generic; // For List<T>

using MonoTouch.Foundation; // Core Foundation types for iOS
using MonoTouch.UIKit;    // UIKit framework for UI elements
using System.Threading.Tasks;
using System.Threading;

namespace TikTok.Views.Profile
{
    // Define a class to represent video data
    public class VideoItem
    {
        public bool IsPinned { get; set; }
        public string ViewCount { get; set; }
        public string ThumbnailUrl { get; set; }
        public string VideoUrl { get; set; }

        public VideoItem(bool isPinned, string thumbnailUrl, string viewCount, string videoUrl)
        {
            IsPinned = isPinned;
            ViewCount = viewCount;
            ThumbnailUrl = thumbnailUrl;
            VideoUrl = videoUrl;
        }
    }

    public class Profile
    {
        public string AvatarUrl { get; set; }
        public string FollowerCount { get; set; }
        public string FollowingCount { get; set; }
        public string HeartCount { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string Description { get; set; }
        public VideoItem[] Videos { get; set; }
    }


    // Dummy VideoViewController class as requested, containing the font definitions
    public static class FontController // Making it static as fonts are typically static/global resources
    {
        private static UIFont _regularFont;
        private static UIFont _boldFont;

        // Static property for a regular font at a specific size (you can adjust sizes as needed)
        public static UIFont RegularFont
        {
            get
            {
                if (_regularFont == null)
                {
                    _regularFont = UIFont.SystemFontOfSize(16); // Default size, will be overridden
                }
                return _regularFont;
            }
            // A setter can be added if you need to dynamically set it, but usually not for global fonts
        }

        // Static property for a bold font at a specific size
        public static UIFont BoldFont
        {
            get
            {
                if (_boldFont == null)
                {
                    _boldFont = UIFont.BoldSystemFontOfSize(16); // Default size, will be overridden
                }
                return _boldFont;
            }
        }

        // Helper method to get a regular font of a specific size
        public static UIFont GetRegularFont(float size)
        {
            return UIFont.SystemFontOfSize(size);
        }

        // Helper method to get a bold font of a specific size
        public static UIFont GetBoldFont(float size)
        {
            return UIFont.BoldSystemFontOfSize(size);
        }
    }

    public class ProfileViewController : UIViewController
    {
        private UIScrollView scrollView;
        private const float BottomTabBarHeight = 50f;
        private Profile _profile;
        private UIActivityIndicatorView _activityLoader;
        private bool self;
        private CancellationTokenSource _cancellationToken;
        private bool _disposed = false;

        private void Run(NSAction action)
        {
            InvokeOnMainThread(action);
        }

        public ProfileViewController(Profile profile) 
        {
            _cancellationToken = new CancellationTokenSource();
            self = true;
            _profile = profile;
            Title = _profile.Name;
        }

        public ProfileViewController(string author)
        {
            Loader(author);
        }

        async private void Loader(string author)
        {
            Title = author;

            try
            {
                _cancellationToken = new CancellationTokenSource();
                _profile = await Atnik.Tiktok.GetProfile(author, _cancellationToken);
            }
            catch
            {
                var alert = new UIAlertView("error", "couldn't load profile", null, "ok");
                alert.Clicked += (object s, UIButtonEventArgs e) =>
                {
                    Close();
                };

                alert.Show();
                return;
            }
            
            Title = _profile.Name;
        }

        public void Close()
        {
            if (_cancellationToken != null)
            {
                _cancellationToken.Cancel();
                _cancellationToken.Dispose();
            }

            Run(() => {
                if (_disposed)
                {
                    return;
                }

                NavigationController.PopViewControllerAnimated(true); Dispose(); 
            });
        }

        public override void ViewWillDisappear(bool animated)
        {
            base.ViewWillDisappear(animated);
        }

        protected override void Dispose(bool disposing)
        {
            System.Diagnostics.Debug.WriteLine("IM BEING DISPOSED AAHHH");

            if (!_disposed)
            {
                if (disposing)
                {
                    if (_cancellationToken != null)
                    {
                        try
                        {
                            _cancellationToken.Cancel();
                            _cancellationToken.Dispose();
                        }
                        catch { }
                        
                        _cancellationToken = null;   
                    }

                    if (scrollView != null)
                    {
                        foreach (var subview in scrollView.Subviews)
                        {
                            var imgView = subview as UIImageView;

                            if (imgView != null)
                            {
                                imgView.Image = null; 
                            }
                            subview.Dispose(); 
                        }
                        scrollView.Dispose();
                        scrollView = null;    
                    }
                    if (_activityLoader != null)
                    {
                        _activityLoader.Dispose();
                        _activityLoader = null;
                    }
                }
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        async public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            if (!self) 
            {
                NavigationItem.LeftBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Stop, (sender, e) =>
                {
                    Close();
                });
            }

            View.BackgroundColor = UIColor.Black;
            View.Frame = new RectangleF(0, 0, View.Frame.Width, View.Frame.Height - TabBarController.TabBar.Frame.Height);

            _activityLoader = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.White)
            {
                Frame = new RectangleF(View.Bounds.Width / 2 - 30, View.Bounds.Height / 2 - 30, 60, 60),
                BackgroundColor = UIColor.Clear,
                HidesWhenStopped = true
            };
            Add(_activityLoader);

            _activityLoader.StartAnimating();

            while (_profile == null)
            {
                await Task.Delay(300);
            }

            LoadProfile();
        }

        public void LoadProfile()
        {
            _activityLoader.StopAnimating();
            // --- 2. Main Scroll View ---
            scrollView = new UIScrollView(new RectangleF(0, 0, View.Frame.Width, View.Frame.Height))
            {
                BackgroundColor = UIColor.Clear
            };
            Add(scrollView);

            float currentY = 10f;

            // --- Profile Header Content ---

            var background = new UIImageView(new RectangleF(0, 0, View.Frame.Width, 160))
            {
                Image = UIImage.FromBundle("background.png")
            };

            scrollView.AddSubview(background);

            // User Avatar
            var avatarSize = 100f;
            var avatarImageView = new UIImageView(new RectangleF((View.Bounds.Width - avatarSize) / 2, currentY, avatarSize, avatarSize))
            {
                BackgroundColor = UIColor.LightGray,
                ContentMode = UIViewContentMode.ScaleAspectFill,
                Layer = { CornerRadius = avatarSize / 2, MasksToBounds = true },
            };
            scrollView.AddSubview(avatarImageView);
            var avatarImageCoverView = new UIImageView(new RectangleF((View.Bounds.Width - avatarSize) / 2, currentY, avatarSize, avatarSize))
            {
                BackgroundColor = UIColor.Clear,
                ContentMode = UIViewContentMode.ScaleAspectFill,
                Layer = { CornerRadius = avatarSize / 2, MasksToBounds = true },
                Alpha = 0.5f
            };
            scrollView.AddSubview(avatarImageCoverView);

            avatarImageCoverView.Image = UIImage.FromFile("profile-avatar-skevo.png");
            Atnik.Tiktok.SetImage((NSData data) =>
            {
                avatarImageView.Image = UIImage.LoadFromData(data);
            }, _profile.AvatarUrl, true, _cancellationToken);

            currentY += avatarSize + 15f;

            // User Tag
            var userTagLabel = new UILabel(new RectangleF(0, currentY, View.Bounds.Width, 20))
            {
                Text = string.Format("@{0}", _profile.Username),
                TextAlignment = UITextAlignment.Center,
                Font = FontController.GetRegularFont(16), // Using specified font helper
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear
            };
            scrollView.AddSubview(userTagLabel);
            currentY += userTagLabel.Frame.Height + 15f;

            var statWidth = View.Bounds.Width / 3;
            var statValueFont = FontController.GetRegularFont(20); // Using specified font helper
            var statLabelFont = FontController.GetBoldFont(12); // Using specified font helper

            var accountStats = new UIImageView(new RectangleF(0, currentY, View.Frame.Width, 60))
            {
                Image = UIImage.FromBundle("profile-info-back.png")
            };
            scrollView.AddSubview(accountStats);

            var statCenter = accountStats.Frame.Height / 2;

            // Following
            var followingValue = new UILabel(new RectangleF(0, statCenter - 25, statWidth, 25))
            {
                Text = _profile.FollowingCount,
                TextAlignment = UITextAlignment.Center,
                Font = statValueFont,
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear
            };
            accountStats.AddSubview(followingValue);
            var followingLabel = new UILabel(new RectangleF(0, statCenter + 10, statWidth, 15))
            {
                Text = "Following",
                TextAlignment = UITextAlignment.Center,
                Font = statLabelFont,
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear
            };
            accountStats.AddSubview(followingLabel);

            // Followers
            var followersValue = new UILabel(new RectangleF(statWidth, statCenter - 25, statWidth, 25))
            {
                Text = _profile.FollowerCount,
                TextAlignment = UITextAlignment.Center,
                Font = statValueFont,
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear
            };
            accountStats.AddSubview(followersValue);

            SizeF followersTextSize = ((NSString)followersValue.Text).StringSize(statValueFont);

            var followersLabel = new UILabel(new RectangleF(statWidth, statCenter + 10, statWidth, 15))
            {
                Text = "Followers",
                TextAlignment = UITextAlignment.Center,
                Font = statLabelFont,
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear
            };
            accountStats.AddSubview(followersLabel);

            // Likes
            var likesValue = new UILabel(new RectangleF(statWidth * 2, statCenter - 25, statWidth, 25))
            {
                Text = _profile.HeartCount,
                TextAlignment = UITextAlignment.Center,
                Font = statValueFont,
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear
            };
            accountStats.AddSubview(likesValue);
            var likesLabel = new UILabel(new RectangleF(statWidth * 2, statCenter + 10, statWidth, 15))
            {
                Text = "Likes",
                TextAlignment = UITextAlignment.Center,
                Font = statLabelFont,
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear
            };
            accountStats.AddSubview(likesLabel);

            var descriptionSplitter = new UIView(new RectangleF(0, accountStats.Frame.Bottom, accountStats.Frame.Width, 2))
            {
                BackgroundColor = UIColor.Gray,
                Alpha = 0.7f
            };

            scrollView.AddSubview(descriptionSplitter);

            var descriptionLabel = new UILabel(new RectangleF(0, 0, 0, 0))
            {
                TextAlignment = UITextAlignment.Center,
                Font = FontController.GetRegularFont(10),
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear,
                LineBreakMode = UILineBreakMode.CharacterWrap,
                Lines = 0
            };

            var padding = 20;
            var availableWidth = accountStats.Frame.Width - (padding * 2);

            descriptionLabel.Text = _profile.Description;
            NSString text = new NSString(descriptionLabel.Text);
            SizeF constrainedSize = new SizeF(availableWidth, float.MaxValue);
            SizeF textSize = text.StringSize(descriptionLabel.Font, constrainedSize, UILineBreakMode.CharacterWrap);

            descriptionLabel.Frame = new RectangleF(padding, 0, availableWidth, textSize.Height + 5);

            var accountDescription = new UIImageView(new RectangleF(0, descriptionSplitter.Frame.Bottom, accountStats.Frame.Width, textSize.Height + 5))
            {
                Image = UIImage.FromBundle("description-background.png")
            };
            accountDescription.AddSubview(descriptionLabel);

            scrollView.AddSubview(accountDescription);

            currentY += accountStats.Frame.Height + accountDescription.Frame.Height + descriptionSplitter.Frame.Height;

            var pinnedVideos = new List<VideoItem>();
            var unpinnedVideos = new List<VideoItem>();
            foreach (var video in _profile.Videos)
            {
                if (video.IsPinned) pinnedVideos.Add(video);
                else unpinnedVideos.Add(video);
            }
            var orderedVideos = new List<VideoItem>();
            orderedVideos.AddRange(pinnedVideos);
            orderedVideos.AddRange(unpinnedVideos);

            float gridPadding = 1f;
            float thumbnailWidth = (View.Bounds.Width - 4 * gridPadding) / 3;
            float thumbnailHeight = thumbnailWidth * 1.33f;

            for (int i = 0; i < orderedVideos.Count; i++)
            {
                int col = i % 3;
                int row = i / 3;

                float x = gridPadding + col * (thumbnailWidth + gridPadding);
                float y = currentY + row * (thumbnailHeight + gridPadding);

                var videoContainer = new UIView(new RectangleF(i == 0 ? 0 : x, y, thumbnailWidth, thumbnailHeight));
                scrollView.AddSubview(videoContainer);

                var thumbnailImageView = new UIImageView(new RectangleF(0, 0, videoContainer.Frame.Width, videoContainer.Frame.Height))
                {
                    BackgroundColor = UIColor.FromRGB(50, 50, 50)
                };
                videoContainer.AddSubview(thumbnailImageView);

                var thumbnailImageViewCover = new UIImageView(new RectangleF(0, 0, thumbnailWidth, thumbnailHeight))
                {
                    Image = UIImage.FromBundle("profile-info-back.png"),
                    Alpha = 0.3f
                };
                videoContainer.AddSubview(thumbnailImageViewCover);

                var viewCountLabelBackground = new UIImageView(new RectangleF(0, thumbnailHeight - 20, thumbnailWidth, 20))
                {
                    Image = UIImage.FromBundle("profile-video-info.png")
                };
                videoContainer.AddSubview(viewCountLabelBackground);

                var viewCountIcon = new UIImageView(new RectangleF(5, thumbnailHeight - 18, 15, 15))
                {
                    Image = UIImage.FromBundle("profile-video-played.png")
                };
                videoContainer.AddSubview(viewCountIcon);

                var viewCountLabel = new UILabel(new RectangleF(25, thumbnailHeight - 20, thumbnailWidth - 25, 20))
                {
                    Text = orderedVideos[i].ViewCount,
                    TextColor = UIColor.White,
                    BackgroundColor = UIColor.Clear,
                    Font = FontController.GetBoldFont(12),
                };
                videoContainer.AddSubview(viewCountLabel);

                if (orderedVideos[i].IsPinned)
                {
                    var pinnedBadge = new UIImageView(new RectangleF(5, 5, 50, 20));
                    pinnedBadge.Image = UIImage.FromBundle("profile-pinned-eng.png");

                    videoContainer.AddSubview(pinnedBadge);
                }

                Atnik.Tiktok.SetImage((NSData data) =>
                {
                    thumbnailImageView.Image = UIImage.LoadFromData(data);
                }, orderedVideos[i].ThumbnailUrl, true, _cancellationToken);
            }

            int numberOfVideoRows = (int)Math.Ceiling((double)orderedVideos.Count / 3);
            float videoGridTotalHeight = numberOfVideoRows * (thumbnailHeight + gridPadding);
            scrollView.ContentSize = new SizeF(View.Bounds.Width, currentY + videoGridTotalHeight + 20);
        }
    }

    public class iItem 
    {
        public string icon;
        public string label;
    }
}