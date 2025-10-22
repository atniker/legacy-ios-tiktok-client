using System;
using System.Collections.Generic;
using System.Drawing; // This is used for RectangleF, PointF, and SizeF
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using MonoTouch.CoreGraphics; // Keeping this for the few types still needed, like float
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.AVFoundation;
using MonoTouch.CoreFoundation;
using MonoTouch.CoreMedia;
using System.Net;

namespace TikTok.Views
{
    // The main view controller that hosts the video feed
    public class VideoViewController : UIViewController
    {
        private readonly List<VideoItem> _videos = new List<VideoItem>();
        public UICollectionView _collectionView;
        private CommentsViewController _commentsVC;
        private bool _isLoadingMore = false;
        private const string VideoCellId = "VideoCell";
        public VideoItem currentVideo;
        public static VideoViewController instance;
        public static string BoldFont = "TikTokSans-Bold";
        public static string RegularFont = "TikTokSans-Regular";
        public VideoDataSource _videoDataSource;
        public VideoDelegate _videoDelegate;
        public bool resumeOnFocus;

        public VideoViewController(List<VideoItem> prebakedVideos) 
        {
            _videos = prebakedVideos;
            instance = this;
            TabBarItem = new UITabBarItem(UITabBarSystemItem.Featured, 0);
            TabBarItem.Title = "Trending";

            var warmItems = new List<Atnik.WarmbootItem>();

            foreach (var i in _videos) 
            {
                warmItems.Add(new Atnik.WarmbootItem() {
                    url = i.VideoUrl,
                    itemId = i.Id
                });
            }

            Atnik.Warmboot.LoadVideos(warmItems.ToArray());
        }

        public void InvokeAsMain(NSAction action) 
        {
            InvokeOnMainThread(action);
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            View.BackgroundColor = UIColor.Black;
            View.Frame = new RectangleF(0, 0, View.Frame.Width, View.Frame.Height - (TabBarController.TabBar.Frame.Height + NavigationController.NavigationBar.Frame.Height));
            _videoDataSource = new VideoDataSource(this, _videos);

            var layout = new UICollectionViewFlowLayout
            {
                ScrollDirection = UICollectionViewScrollDirection.Vertical,
                MinimumLineSpacing = 0,
                MinimumInteritemSpacing = 0,
                ItemSize = View.Bounds.Size
            };

            _collectionView = new UICollectionView(View.Bounds, layout)
            {
                BackgroundColor = UIColor.Black,
                PagingEnabled = true,
                ShowsVerticalScrollIndicator = false,
                // Do NOT set DataSource and Delegate here yet.
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
            };

            // Then, add it to the view hierarchy
            View.AddSubview(_collectionView);

            // Now that _collectionView is fully instantiated and part of the view hierarchy,
            // create and assign the DataSource and Delegate.
            // Pass _collectionView itself to the delegate constructor now.
            _videoDataSource = new VideoDataSource(this, _videos); // Assuming _videoDataSource needs _videos
            _videoDelegate = new VideoDelegate(this, _videos, _collectionView);
            _collectionView.DataSource = _videoDataSource;
            _collectionView.Delegate = _videoDelegate; // This is safe now.

            _collectionView.RegisterClassForCell(typeof(VideoCell), new NSString(VideoCellId));

            // Start loading the initial set of videos asynchronously
            //Task.Run(async () => await LoadMoreVideosAsync());
        }

        // Presents the comments view controller with a custom animation
        public void PresentCommentsView()
        {
            // The bug was here: _commentsVC was only created once and not reset.
            // This fix always creates a new instance to ensure it can be opened multiple times.
            _commentsVC = new CommentsViewController(currentVideo.Author.Username, currentVideo.Id);
            _commentsVC.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;

            // Present with a vertical slide-up animation
            PresentViewController(_commentsVC, true, null);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            if (resumeOnFocus)
            {
                var cell = _videoDelegate._currentlyPlayingCell;

                if (cell != null)
                {
                    if (cell.Player == null)
                    {
                        return;
                    }

                    cell.Player.Play();
                }
            }

            resumeOnFocus = false;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            var cell = _videoDelegate._currentlyPlayingCell;

            if (this.PresentedViewController != null)
            {
                if ((PresentedViewController as CommentsViewController) != null)
                {
                    return;
                }
            }

            if (cell != null)
            {
                if (cell.Player == null)
                {
                    return;
                }

                if (cell.Player.Rate == 0)
                {
                    return;
                }

                cell.Player.Pause();
                resumeOnFocus = true;
            }
        }

        public void PresentOptionsView()
        {
            // The currentVideo should already be set by VideoDataSource.GetCell
            // when a VideoCell becomes active.
            if (currentVideo == null)
            {
                System.Diagnostics.Debug.WriteLine("Warning: currentVideo is null when trying to open options.");
                return;
            }

            // Create a new instance to ensure it can be opened multiple times
            // Pass currentVideo to the OptionsViewController so it knows which video's options are being accessed.
            var optionsVC = new OptionsViewController(currentVideo.Id, currentVideo.VideoUrl);
            optionsVC.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;

            // Present with a vertical slide-up animation
            PresentViewController(optionsVC, true, null);
        }

        // Asynchronously loads more video data
        public async Task LoadMoreVideosAsync()
        {
            if (_isLoadingMore) return;
            _isLoadingMore = true;

            try
            {
                // Step 1: Fetch new videos asynchronously
                var fetchedNewVideos = await Atnik.Tiktok.GetForYouPage();

                // Step 2: Warmboot the new videos
                var warmVideos = new List<Atnik.WarmbootItem>();
                foreach (var i in fetchedNewVideos)
                {
                    warmVideos.Add(new Atnik.WarmbootItem()
                    {
                        url = i.VideoUrl,
                        itemId = i.Id
                    });
                }
                Atnik.Warmboot.LoadVideos(warmVideos.ToArray());

                // Step 3: All data source modifications and UICollectionView updates MUST be on the main thread
                InvokeOnMainThread(() =>
                {
                    // Capture the number of videos to remove before modifying _videos
                    int numberOfVideosToRemove = _videos.Count / 2;
                    var videoIdsToCleanupFromWarmboot = new List<string>();

                    // Ensure we don't remove currently visible items unless explicitly desired
                    // Adjust numberOfVideosToRemove based on visible items
                    var firstVisibleIndexPath = _collectionView.IndexPathsForVisibleItems.OrderBy(ip => ip.Row).FirstOrDefault();
                    int firstVisibleRow = firstVisibleIndexPath != null ? (int)firstVisibleIndexPath.Row : 0;
                    // Only remove items that are strictly above the first visible item
                    numberOfVideosToRemove = Math.Min(numberOfVideosToRemove, firstVisibleRow);

                    // Capture the starting index for new videos *after* deletions
                    // This will be the index of the first new video once added
                    int startIndexForNewVideos = _videos.Count - numberOfVideosToRemove; // Or simply _videos.Count if removals happen first

                    _collectionView.PerformBatchUpdates(() =>
                    {
                        // --- Part A: Handle Deletions First ---
                        if (numberOfVideosToRemove > 0)
                        {
                            for (int i = 0; i < numberOfVideosToRemove; i++)
                            {
                                videoIdsToCleanupFromWarmboot.Add(_videos[i].Id);
                            }
                            _videos.RemoveRange(0, numberOfVideosToRemove);
                            _collectionView.DeleteItems(Enumerable.Range(0, numberOfVideosToRemove).Select(i => NSIndexPath.FromItemSection(i, 0)).ToArray());

                            // Perform Atnik.Warmboot cleanup for these video IDs
                            Atnik.Warmboot.Cleanup(videoIdsToCleanupFromWarmboot.ToArray());
                        }

                        // --- Part B: Handle Insertions ---
                        // IMPORTANT: The startIndexForNewVideos calculation must be correct relative to the list *after* deletions
                        // Correctly calculate startIndexForNewVideos based on the _videos.Count *after* the removal
                        // If no items were removed, startIndexForNewVideos will be _videos.Count (which is initialVideosCount).
                        // If items were removed, it will be initialVideosCount - numberOfVideosToRemove.
                        startIndexForNewVideos = _videos.Count; // This is the current count after deletions

                        _videos.AddRange(fetchedNewVideos);

                        var indexPathsToInsert = new List<NSIndexPath>();
                        for (int i = 0; i < fetchedNewVideos.Count; i++)
                        {
                            indexPathsToInsert.Add(NSIndexPath.FromItemSection(startIndexForNewVideos + i, 0));
                        }
                        _collectionView.InsertItems(indexPathsToInsert.ToArray());

                    }, (completed) => // This completion handler runs AFTER the batch updates are finished
                    {
                        if (completed && fetchedNewVideos.Count > 0)
                        {
                            // --- CRITICAL ADDITION: Scroll to the first new item ---
                            var firstNewVideoIndexPath = NSIndexPath.FromItemSection(startIndexForNewVideos, 0);

                            // Scroll to make the first new item visible, preferably at the top
                            // Use 'animated: false' for a seamless transition, or 'true' if you want to see the scroll.
                            // For continuous feed, 'false' is often preferred.
                            _collectionView.ScrollToItem(firstNewVideoIndexPath, UICollectionViewScrollPosition.Top, false);

                            // Note: This will trigger the Scrolled delegate, which will then determine the
                            // new center video for playback. If your screen shows only one video at a time,
                            // and you scroll to UICollectionViewScrollPosition.Top, the video that comes
                            // to the top of the screen might also be detected as the center video (due to rounding).
                            // If your expectation is that the *very first* video from fetchedNewVideos starts playing,
                            // this scroll will ensure it's on screen and your Scrolled logic should then handle playing it.
                        }
                        _isLoadingMore = false;
                    });
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error loading videos: {0}", ex.Message));
                _isLoadingMore = false;
            }
        }

        public Action GetProfileOpener(string author)
        {
            return () =>
            {
                var profileController = new Profile.ProfileViewController(author);
                NavigationController.PushViewController(profileController, true);
            };
        }
    }

    // A simple data model for video content
    public class VideoItem
    {
        public Author Author { get; set; }
        public long CreateTime { get; set; }
        public string Description { get; set; }
        public string Hearts { get; set; }
        public string CommentCount { get; set; }
        public string Id { get; set; }
        public string VideoUrl { get; set; }
        public int Duration { get; set; }
    }

    public class Author
    {
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string Followers { get; set; }
        public string Following { get; set; }
        public string Hearts { get; set; }
        public bool Verified { get; set; }
    }

    // The data source for the UICollectionView
    public class VideoDataSource : UICollectionViewDataSource
    {
        private readonly VideoViewController _viewController;
        private readonly List<VideoItem> _videos;
        private const string VideoCellId = "VideoCell";

        public VideoDataSource(VideoViewController vc, List<VideoItem> videos)
        {
            _viewController = vc;
            _videos = videos;
        }

        public override int GetItemsCount(UICollectionView collectionView, int section)
        {
            return _videos.Count;
        }

        public NSIndexPath RemoveVideo(VideoItem videoToRemove)
        {
            int index = _videos.IndexOf(videoToRemove);
            if (index != -1)
            {
                _videos.RemoveAt(index);
                return NSIndexPath.FromRowSection(index, 0);
            }
            return null;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (VideoCell)collectionView.DequeueReusableCell(new NSString(VideoCellId), indexPath);
            var video = _videos[indexPath.Row];
            _viewController.currentVideo = video;
            cell.SetupVideo(_viewController, video);

            return cell;
        }
    }

    // The delegate for handling UICollectionView events
    public class VideoDelegate : UICollectionViewDelegate
    {
        private readonly VideoViewController _viewController;
        private readonly List<VideoItem> _videos;
        public VideoCell _currentlyPlayingCell;
        private NSIndexPath _currentlyPlayingIndexPath;
        private UICollectionView _collectionViewRef;

        public VideoDelegate(VideoViewController vc, List<VideoItem> videos, UICollectionView collectionView)
        {
            _viewController = vc;
            _videos = videos;
            _collectionViewRef = collectionView;
        }

        public override void Scrolled(UIScrollView scrollView)
        {
            var offset = scrollView.ContentOffset.Y;
            var height = scrollView.Frame.Height;

            // Load more videos logic (keep this)
            var preLastIndex = _videos.Count - 2;
            if (offset / height >= preLastIndex)
            {
                Task.Run(async () => await _viewController.LoadMoreVideosAsync());
            }

            // --- NEW: Playback logic based on current central cell ---
            // Find the current central cell based on content offset
            var centerPoint = new PointF(_collectionViewRef.Center.X, _collectionViewRef.ContentOffset.Y + _collectionViewRef.Bounds.Height / 2);
            var centerIndexPath = _collectionViewRef.IndexPathForItemAtPoint(centerPoint);

            // Only proceed if a new central cell is identified, or if the current one is somehow invalid
            if (centerIndexPath != null && !centerIndexPath.Equals(_currentlyPlayingIndexPath))
            {
                // Get the new cell that should be playing
                var newCenterCell = (VideoCell)_collectionViewRef.CellForItem(centerIndexPath);

                if (newCenterCell != null)
                {
                    // Pause the previously playing cell if it exists and is different from the new center cell
                    if (_currentlyPlayingCell != null && _currentlyPlayingCell != newCenterCell && _currentlyPlayingCell.Player != null)
                    {
                        _currentlyPlayingCell.Player.Pause();
                        _currentlyPlayingCell.Player.Seek(CMTime.Zero); // Rewind for next time
                    }

                    // Set the new center cell as the currently playing one
                    _currentlyPlayingCell = newCenterCell;
                    _currentlyPlayingIndexPath = centerIndexPath;
                    _viewController.currentVideo = _videos[centerIndexPath.Row]; // Update current video for comments etc.

                    // Try to play this video. It might not be ready yet.
                    if (newCenterCell.Player != null && newCenterCell.Player.CurrentItem != null && newCenterCell.Player.CurrentItem.Status == AVPlayerItemStatus.ReadyToPlay)
                    {
                        newCenterCell.Player.Play();
                    }
                    else
                    {
                        // Video not ready, it will play once ReadyToPlay status is observed in VideoCell.ObserveValue.
                        newCenterCell.ShowLoader(true); // Ensure loader is visible while waiting
                    }
                }
            }
            else if (centerIndexPath == null && _currentlyPlayingCell != null)
            {
                // If no cell is found in the center (e.g., during a very fast scroll or bounce), pause the current one.
                _currentlyPlayingCell.Player.Pause();
                _currentlyPlayingCell.Player.Seek(CMTime.Zero);
                _currentlyPlayingCell = null;
                _currentlyPlayingIndexPath = null;
            }
    }

    // WillDisplayCell: NO LONGER CALL Player.Play() HERE.
    // Its main role is to ensure SetupVideo is called by GetCell (which happens before this)
    // and that the cell is prepared for display.
    [Export("collectionView:willDisplayCell:forItemAtIndexPath:")]
    public void WillDisplayCell(UICollectionView collectionView, UICollectionViewCell cell, NSIndexPath indexPath)
    {
    }

    // DidEndDisplayingCell: Crucial for pausing and cleaning up when a cell moves off-screen
    [Export("collectionView:didEndDisplayingCell:forItemAtIndexPath:")]
    public void DidEndDisplayingCell(UICollectionView collectionView, UICollectionViewCell cell, NSIndexPath indexPath)
    {
        var videoCell = cell as VideoCell;

        videoCell.CleanupForReuse(); // Ensure cleanup for cells scrolling off

        // If the cell that just went off-screen was the one marked as currently playing, clear it
        if (_currentlyPlayingCell == videoCell)
        {
            _currentlyPlayingCell = null;
            _currentlyPlayingIndexPath = null;
        }
    }

    // Helper method for VideoCell to check if it's the currently playing one
    public bool IsThisCellCurrentlyPlaying(VideoCell cell)
    {
        return _currentlyPlayingCell == cell;
    }
}

    // The custom cell for each video feed item
    public class VideoCell : UICollectionViewCell
    {
        public AVPlayer Player { get; private set; }
        private AVPlayerLayer _playerLayer;
        private UIButton _likeButton;
        private UIButton _commentButton;
        private UIButton _pauseButton;
        private UILabel _likeCountLabel;
        private UILabel _commentCountLabel;
        private UILabel _descriptionLabel;
        private UILabel _authorNameLabel;
        private UIButton _authorAvatar;
        private UIProgressView _progressSlider;
        private VideoViewController _parentVC;
        private NSObject _timeObserver;
        private UIActivityIndicatorView _loadingIndicator;
        private Action _openCurrentProfile;
        private UILabel _loadingError;
        private CancellationTokenSource _videoCancellationToken;
        private bool _disposed_VideoCell = false;
        private NSObject _loopObserverAddedMarker;
        private UIButton _optionsButton;

        [Export("initWithFrame:")]
        public VideoCell(RectangleF frame)
            : base(frame)
        {
            InitializeViews();
            var tapGesture = new UITapGestureRecognizer(this, new MonoTouch.ObjCRuntime.Selector("onVideoTapped:"));
            ContentView.AddGestureRecognizer(tapGesture);
        }

        private void InitializeViews()
        {
            _playerLayer = new AVPlayerLayer
            {
                Frame = ContentView.Bounds,
                LayerVideoGravity = AVLayerVideoGravity.ResizeAspectFill,
                MasksToBounds = true
            };
            ContentView.ClipsToBounds = true;
            ContentView.Layer.AddSublayer(_playerLayer);

            _pauseButton = new UIButton(UIButtonType.Custom)
            {
                Frame = new RectangleF(ContentView.Bounds.Width / 2 - 30, ContentView.Bounds.Height / 2 - 30, 60, 60),
                Alpha = 0,
            };

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.White)
            {
                Frame = new RectangleF(ContentView.Bounds.Width / 2 - 30, ContentView.Bounds.Height / 2 - 30, 60, 60),
                BackgroundColor = UIColor.Clear,
                HidesWhenStopped = true
            };

            _loadingError = new UILabel()
            {
                Frame = new RectangleF(ContentView.Bounds.Width / 2 - 30, ContentView.Bounds.Height / 2 - 30, ContentView.Bounds.Width, 60),
                BackgroundColor = UIColor.Clear,
                TextColor = UIColor.White,
                Font = UIFont.FromName(VideoViewController.RegularFont, 10)
            };

            var font = UIFont.FromName(VideoViewController.BoldFont, 14);

            _pauseButton.SetImage(UIImage.FromBundle("pause.png"), UIControlState.Normal);
            //UIColor._pauseButton.TintColor = UIColor.White;
            _pauseButton.BackgroundColor = UIColor.Clear;
            ContentView.AddSubview(_pauseButton);
            ContentView.AddSubview(_loadingIndicator);
            ContentView.AddSubview(_loadingError);

            _authorAvatar = new UIButton(UIButtonType.Custom);
            _authorAvatar.Frame = new RectangleF(ContentView.Bounds.Width - 60, ContentView.Bounds.Height - 260, 50, 50);
            _authorAvatar.Layer.CornerRadius = 25;
            _authorAvatar.ClipsToBounds = true;
            _authorAvatar.BackgroundColor = UIColor.Gray;
            ContentView.AddSubview(_authorAvatar);

            _authorNameLabel = new UILabel(new RectangleF(20, ContentView.Bounds.Height - 140, ContentView.Bounds.Width - 80, 20));
            _authorNameLabel.TextColor = UIColor.White;
            _authorNameLabel.Font = UIFont.FromName(VideoViewController.BoldFont, 16);
            _authorNameLabel.BackgroundColor = UIColor.Clear;
            ContentView.AddSubview(_authorNameLabel);

            _descriptionLabel = new UILabel(new RectangleF(20, ContentView.Bounds.Height - 120, ContentView.Bounds.Width - 80, 20));
            _descriptionLabel.TextColor = UIColor.White;
            _descriptionLabel.Font = UIFont.FromName(VideoViewController.RegularFont, 14);
            _descriptionLabel.BackgroundColor = UIColor.Clear;
            ContentView.AddSubview(_descriptionLabel);

            _likeButton = new UIButton(UIButtonType.Custom)
            {
                Frame = new RectangleF(ContentView.Bounds.Width - 60, ContentView.Bounds.Height - 200, 50, 50)
            };
            _likeButton.SetTitleColor(UIColor.White, UIControlState.Normal);
            _likeButton.BackgroundColor = UIColor.Clear;
            _likeButton.SetImage(UIImage.FromBundle("heart.png"), UIControlState.Normal);
            ContentView.AddSubview(_likeButton);

            _likeCountLabel = new UILabel(new RectangleF(ContentView.Bounds.Width - 60, ContentView.Bounds.Height - 150, 50, 20));
            _likeCountLabel.TextColor = UIColor.White;
            _likeCountLabel.TextAlignment = UITextAlignment.Center;
            _likeCountLabel.BackgroundColor = UIColor.Clear;
            _likeCountLabel.Font = font;
            ContentView.AddSubview(_likeCountLabel);

            _commentButton = new UIButton(UIButtonType.Custom)
            {
                Frame = new RectangleF(ContentView.Bounds.Width - 60, ContentView.Bounds.Height - 120, 50, 50)
            };
            _commentButton.SetTitleColor(UIColor.White, UIControlState.Normal);
            _commentButton.BackgroundColor = UIColor.Clear;
            _commentButton.SetImage(UIImage.FromBundle("comment.png"), UIControlState.Normal);
            ContentView.AddSubview(_commentButton);

            _commentCountLabel = new UILabel(new RectangleF(ContentView.Bounds.Width - 60, ContentView.Bounds.Height - 70, 50, 20));
            _commentCountLabel.TextColor = UIColor.White;
            _commentCountLabel.TextAlignment = UITextAlignment.Center;
            _commentCountLabel.BackgroundColor = UIColor.Clear;
            _commentCountLabel.Font = font;
            ContentView.AddSubview(_commentCountLabel);

            _optionsButton = new UIButton(UIButtonType.Custom)
            {
                Frame = new RectangleF(ContentView.Bounds.Width - 60, ContentView.Bounds.Height - 340, 50, 50) // Adjust position as needed
            };
            _optionsButton.SetImage(UIImage.FromBundle("share.png"), UIControlState.Normal); // You'll need an image named options_icon.png
            _optionsButton.BackgroundColor = UIColor.Clear;
            //ContentView.AddSubview(_optionsButton);

            _progressSlider = new UIProgressView(UIProgressViewStyle.Default)
            {
                Frame = new RectangleF(0, ContentView.Bounds.Height - 5, ContentView.Bounds.Width, 5)
            };
            //_progressSlider.TintColor = UIColor.White;
            ContentView.AddSubview(_progressSlider);
        }

        public void RunInMainThread(Action action)
        {
            InvokeOnMainThread(() => { action.Invoke(); });
        }

        async public Task LoadVideo(string id, CancellationToken token)
        {
            RunInMainThread(() => { ShowLoader(true); });

            try
            {
            // --- CRITICAL CHANGE: Await the Warmboot signal instead of polling ---
                await Atnik.Warmboot.WaitForVideoReadyAsync(id, token);
                System.Diagnostics.Debug.WriteLine("Video {id} is warmbooted and ready for playback.");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine(id + " cancelled during warmboot wait.");
                RunInMainThread(() =>
                {
                    _loadingError.Text = "Video load cancelled.";
                    _loadingError.Hidden = false;
                    ShowLoader(false);
                });
                return; 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error waiting for {id} warmboot: {ex.Message}");
                RunInMainThread(() =>
                {
                    _loadingError.Text = "Failed to load video: {ex.Message}";
                    _loadingError.Hidden = false;
                    ShowLoader(false);
                });
                return; // Exit on error
            }

            AVPlayerItem playerItem = AVPlayerItem.FromAsset(AVUrlAsset.Create(NSUrl.FromFilename(Atnik.Warmboot.GetItemPath(id)), new AVUrlAssetOptions()));
            playerItem.AddObserver(this, new NSString("status"), NSKeyValueObservingOptions.New, IntPtr.Zero);
            Player = new AVPlayer(playerItem);
            _playerLayer.Player = Player;

            token.ThrowIfCancellationRequested();

            var interval = new CMTime(1, 1);
            if (_timeObserver != null)
            {
                try
                {
                    _timeObserver.Dispose();
                    Player.RemoveTimeObserver(_timeObserver);
                }
                catch { }
            }
            _timeObserver = Player.AddPeriodicTimeObserver(interval, DispatchQueue.MainQueue, (time) =>
            {
                if (Player.CurrentItem != null && !Player.CurrentItem.Duration.IsInvalid)
                {
                    _progressSlider.Progress = (float)(time.Seconds / Player.CurrentItem.Duration.Seconds);
                }
            });

            if (_loopObserverAddedMarker != null) 
            {
                try
                {
                    NSNotificationCenter.DefaultCenter.RemoveObserver(
                        _loopObserverAddedMarker, 
                        AVPlayerItem.DidPlayToEndTimeNotification, 
                        null 
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Error removing old loop observer: {0}", ex.Message));
                }
                _loopObserverAddedMarker = null;
            }

            NSNotificationCenter.DefaultCenter.AddObserver(
                this, 
                new MonoTouch.ObjCRuntime.Selector("handlePlayerItemDidPlayToEndTime:"), 
                AVPlayerItem.DidPlayToEndTimeNotification, 
                playerItem 
            );

            _loopObserverAddedMarker = this; 

            RunInMainThread(() => { ShowLoader(false); });
        }

        async public void SetupVideo(VideoViewController parentVC, VideoItem video)
        {
            _parentVC = parentVC;

            if (_playerLayer.Player != null)
            {
                _playerLayer.Player.Pause();

                if (_playerLayer.Player.CurrentItem != null)
                {
                    _playerLayer.Player.CurrentItem.Dispose();
                }
                
                _playerLayer.Player.Dispose();
                Player = null;
            }

            if (_authorAvatar != null) { _authorAvatar.TouchUpInside += OnAuthorAvatarTapped; }
            if (_likeButton != null) { _likeButton.TouchUpInside += OnLikeButtonTapped; }
            if (_commentButton != null) { _commentButton.TouchUpInside += OnCommentButtonTapped; }
            //if (_optionsButton != null) { _optionsButton.TouchUpInside += OnOptionsButtonTapped; }

            _loadingError.Hidden = true;

            _openCurrentProfile = _parentVC.GetProfileOpener(video.Author.Username);

            _authorNameLabel.Text = string.Format("@{0}", video.Author.Username);
            _descriptionLabel.Text = video.Description;
            _likeCountLabel.Text = video.Hearts.ToString();
            _commentCountLabel.Text = video.CommentCount.ToString();

            if (_videoCancellationToken != null)
            {
                _videoCancellationToken.Cancel();
                _videoCancellationToken.Dispose();
            }

            _videoCancellationToken = new CancellationTokenSource();

            Atnik.Tiktok.SetImage((NSData data) =>
            {
                _authorAvatar.SetImage(UIImage.LoadFromData(data), UIControlState.Normal);
            }, video.Author.Avatar, false, _videoCancellationToken);

            Task.Run(() => LoadVideo(video.Id, _videoCancellationToken.Token), _videoCancellationToken.Token);
        }


        [Export("handlePlayerItemDidPlayToEndTime:")]
        private void HandlePlayerItemDidPlayToEndTime(NSNotification notification)
        {
            var finishedPlayerItem = notification.Object as AVPlayerItem;
            if (finishedPlayerItem != null && Player != null && Player.CurrentItem == finishedPlayerItem)
            {
                RunInMainThread(() =>
                {
                    Player.Seek(CMTime.Zero); 
                    Player.Play();          
                });
            }
        }

        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            // Important: Do not call base.ObserveValue(). Apple's docs say not to.
            // base.ObserveValue(keyPath, ofObject, change, context); // <- REMOVE THIS LINE IF PRESENT

            if (keyPath == "status")
            {
                var playerItem = ofObject as AVPlayerItem;
                if (playerItem != null && playerItem.Handle != IntPtr.Zero && Player != null && Player.Handle != IntPtr.Zero && Player.CurrentItem == playerItem)
                {
                    switch (playerItem.Status)
                    {
                        case AVPlayerItemStatus.ReadyToPlay:
                            RunInMainThread(() => { ShowLoader(false); }); // Hide loader once ready

                            // --- NEW CRITICAL CHECK ---
                            // ONLY play if this cell is currently designated as the "active" one by the delegate.
                            VideoDelegate videoDelegate = _parentVC._collectionView.Delegate as VideoDelegate;
       
                            if (videoDelegate.IsThisCellCurrentlyPlaying(this))
                            {
                                Player.Play();
                            }
                            else
                            {
                                // If it's ready but not the currently playing cell, ensure it's paused and rewound.
                                Player.Pause();
                                Player.Seek(CMTime.Zero);
                            }

                            break;

                        case AVPlayerItemStatus.Failed:
                            RunInMainThread(() =>
                            {
                                _loadingError.Text = playerItem.Error.Description;
                                _loadingError.Hidden = false;
                                ShowLoader(false);
                            });
                            // Also pause if failed
                            Player.Pause();
                            Player.Seek(CMTime.Zero);
                            break;

                        case AVPlayerItemStatus.Unknown:
                           break;
                    }
                }
                else
                {
                }
            }
        }

        public void ShowLoader(bool toggle)
        {
            if (toggle)
            {
                _loadingIndicator.StartAnimating();
            }
            else
            {
                _loadingIndicator.StopAnimating();
            }
        }

        [Export("onVideoTapped:")]
        public void OnVideoTapped(UITapGestureRecognizer recognizer)
        {
            if (Player == null) return;
            if (Player.Rate == 0)
            {
                Player.Play();
                UIView.Animate(0.3, () => _pauseButton.Alpha = 0);
            }
            else
            {
                Player.Pause();
                UIView.Animate(0.3, () => _pauseButton.Alpha = 1);
            }
        }

        private void OnLikeButtonTapped(object sender, EventArgs e)
        {
            // Logic for handling a like
        }

        private void OnAuthorAvatarTapped(object sender, EventArgs e)
        {
            _openCurrentProfile.Invoke();
        }

        private void OnCommentButtonTapped(object sender, EventArgs e)
        {
            _parentVC.PresentCommentsView();
        }

        private void OnOptionsButtonTapped(object sender, EventArgs e)
        {
            // Call the parent ViewController to present the options menu
            _parentVC.PresentOptionsView();
        }

        public void CleanupForReuse()
        {
                        // 1. Cancel ongoing video loading task (Already good)
            if (_videoCancellationToken != null)
            {
                _videoCancellationToken.Cancel();
                _videoCancellationToken.Dispose();
                _videoCancellationToken = null;
            }

            if (_loopObserverAddedMarker != null)
            {
                try
                {
                    NSNotificationCenter.DefaultCenter.RemoveObserver(
                        _loopObserverAddedMarker,
                        AVPlayerItem.DidPlayToEndTimeNotification, 
                        null 
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Error removing AVPlayerItem DidPlayToEndTime observer: {0}", ex.Message));
                }
                finally
                {
                    _loopObserverAddedMarker = null; 
                }
            }

            if (Player != null && Player.CurrentItem != null && Player.CurrentItem.Handle != IntPtr.Zero)
            {
                try
                {
                    Player.CurrentItem.RemoveObserver(this, new NSString("status"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error removing AVPlayerItem observer: {ex.Message}");
                }
            }

            // 3. Remove the periodic time observer (Already good)
            if (_timeObserver != null)
            {
                try
                {
                    if (Player != null && Player.Handle != IntPtr.Zero)
                    {
                        Player.RemoveTimeObserver(_timeObserver);
                    }
                }
                catch (Exception ex) { Console.WriteLine("Error removing time observer: {ex.Message}"); }
                finally
                {
                    _timeObserver.Dispose();
                    _timeObserver = null;
                }
            }

            // --- IMPORTANT: Detach player from layer *before* disposing player itself ---
            // This order might be safer if the layer binding itself is fragile.
            if (_playerLayer != null)
            {
                try
                {
                    if (_playerLayer.Handle != IntPtr.Zero)
                    {
                        if (_playerLayer.Player != null) // Only try to set null if it currently has a player
                        {
                            _playerLayer.Player = null; // Detach player from layer FIRST
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: _playerLayer.Handle is IntPtr.Zero in CleanupForReuse before detaching player.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error detaching player from layer during pre-disposal: {ex.Message}");
                }
            }


            // 4. Dispose the AVPlayer and its CurrentItem
            if (Player != null)
            {
                try
                {
                    if (Player.Handle != IntPtr.Zero)
                    {
                        Player.Pause(); // Always pause before releasing
                    }
                }
                catch (Exception ex) { Console.WriteLine("Error pausing AVPlayer: {ex.Message}"); }

                try
                {
                    if (Player.Handle != IntPtr.Zero && Player.CurrentItem != null && Player.CurrentItem.Handle != IntPtr.Zero)
                    {
                        Player.ReplaceCurrentItemWithPlayerItem(null); // Release AVPlayerItem
                    }
                }
                catch (Exception ex) { Console.WriteLine("Error replacing AVPlayerItem: {ex.Message}"); }

                try
                {
                    if (Player.Handle != IntPtr.Zero)
                    {
                        Player.Dispose(); // Explicitly dispose the native AVPlayer object
                    }
                }
                catch (Exception ex) { Console.WriteLine("Error disposing AVPlayer: {ex.Message}"); }
                finally
                {
                    Player = null; // Clear the managed reference
                }
            }

            // 5. Unsubscribe from button events (Already good, but ensure all are there)
            if (_authorAvatar != null) { _authorAvatar.TouchUpInside -= OnAuthorAvatarTapped; }
            if (_likeButton != null) { _likeButton.TouchUpInside -= OnLikeButtonTapped; }
            if (_commentButton != null) { _commentButton.TouchUpInside -= OnCommentButtonTapped; }
            if (_optionsButton != null) { _optionsButton.TouchUpInside -= OnOptionsButtonTapped; }
            _openCurrentProfile = null; // Null out the reference to the captured lambda

            // 6. Null out UIImage references on UIImageViews to help release native image data
            if (_authorAvatar != null) { _authorAvatar.SetImage(null, UIControlState.Normal); }

            // 7. Reset UI elements to their default "empty" state (ensure on main thread)
            InvokeOnMainThread(() =>
            {
                _progressSlider.Progress = 0;
                _pauseButton.Alpha = 0;
                ShowLoader(false);
                _loadingError.Hidden = true;
                _loadingError.Text = "";
                _authorNameLabel.Text = "";
                _descriptionLabel.Text = "";
                _likeCountLabel.Text = "";
                _commentCountLabel.Text = "";
            });
        }

        // Dispose override for VideoCell
        protected override void Dispose(bool disposing)
        {
            System.Diagnostics.Debug.WriteLine("dispose me daddy");

            if (!_disposed_VideoCell)
            {
                if (disposing)
                {
                    // Ensure CleanupForReuse is called.
                    // It handles the managed IDisposable items and most native resource cleanup.
                    CleanupForReuse();

                    // Any other managed IDisposable instance fields not covered by CleanupForReuse
                    // (There don't seem to be any unique ones here)

                    // UI elements are subviews of ContentView. Their native resources are typically
                    // released when the base.Dispose() is called, as they are part of the native hierarchy.
                }
                _disposed_VideoCell = true; // Mark as disposed
            }
            base.Dispose(disposing); // Always call base.Dispose to release native resources
        }

        // PrepareForReuse should also call CleanupForReuse
        public override void PrepareForReuse()
        {
            base.PrepareForReuse();
            CleanupForReuse();
        }
    }

    // A data model for a single comment with optional replies
    public class CommentItem
    {
        public string Username { get; set; }
        public string Text { get; set; }
        public string Date { get; set; }
        public string LikeCount { get; set; }
        public List<CommentItem> Replies { get; set; }
        public bool ShowReplies { get; set; }
        public string AvatarUrl { get; set; }
        public string ProfileUrl { get; set; }
    }

    // The view controller for the comments section
    public class CommentsViewController : UIViewController
    {
        private UITableView _tableView;
        private UIActivityIndicatorView _activityIndicator;
        private List<CommentItem> _comments = new List<CommentItem>();
        private string _author;
        private string _videoId;

        public CommentsViewController(string author, string video_id) 
        {
            _author = author;
            _videoId = video_id;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            View.BackgroundColor = UIColor.FromRGBA(0, 0, 0, 0.5f); // Semi-transparent black background

            // Container view for the comment section
            var containerRect = new RectangleF(0, View.Bounds.Height / 3, View.Bounds.Width, View.Bounds.Height * 2 / 3);
            var containerView = new UIView(containerRect);
            containerView.BackgroundColor = UIColor.White;
            containerView.Layer.CornerRadius = 16;
            containerView.ClipsToBounds = true;
            View.AddSubview(containerView);

            // Title label
            var titleLabel = new UILabel(new RectangleF(16, 8, 100, 30));
            titleLabel.Text = "Comments";
            titleLabel.Font = UIFont.FromName(VideoViewController.BoldFont, 18);
            containerView.AddSubview(titleLabel);

            // Close button
            var closeButton = new UIButton(UIButtonType.System)
            {
                Frame = new RectangleF(containerView.Bounds.Width - 40, 8, 30, 30),
                TintColor = UIColor.Gray
            };
            closeButton.SetTitle("✖️", UIControlState.Normal);
            closeButton.TouchUpInside += (sender, e) => DismissViewController(true, null);
            containerView.AddSubview(closeButton);

            _tableView = new UITableView(new RectangleF(0, 40, containerView.Bounds.Width, containerView.Bounds.Height - 40));
            _tableView.BackgroundColor = UIColor.White;
            _tableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            _tableView.RegisterClassForCellReuse(typeof(CommentCell), new NSString("CommentCell"));
            _tableView.Hidden = true; // Removed _tableView.EstimatedRowHeight = 80;
            containerView.AddSubview(_tableView);

            _activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge)
            {
                Color = UIColor.Black,
                Center = new PointF(containerView.Bounds.Width / 2, containerView.Bounds.Height / 2)
            };
            _activityIndicator.StartAnimating();
            containerView.AddSubview(_activityIndicator);

            Task.Run(async () => await LoadCommentsAsync());
        }

        private async Task LoadCommentsAsync()
        {
            _comments = await Atnik.Tiktok.GetComments(_author, _videoId);

            InvokeOnMainThread(() =>
            {
                _activityIndicator.StopAnimating();
                _activityIndicator.Hidden = true;
                _tableView.Hidden = false;
                _tableView.DataSource = new CommentsDataSource(_comments, _tableView);
                _tableView.Delegate = new CommentsDelegate(_comments, _tableView);
                _tableView.ReloadData();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tableView != null)
                {
                    _tableView.DataSource = null; 
                    _tableView.Delegate = null;   
                    _tableView.Dispose();         
                    _tableView = null;
                }
                if (_activityIndicator != null) { _activityIndicator.Dispose(); _activityIndicator = null; }
            }
            base.Dispose(disposing);
        }
    }

    // A custom UITableViewCell for comments
    public class CommentCell : UITableViewCell
    {
        private UILabel _usernameLabel;
        private UILabel _dateLabel;
        private UILabel _commentTextLabel;
        private UILabel _likeCountLabel;
        private UIButton _seeRepliesButton;
        private CommentItem _comment;
        private UIView _repliesContainerView; // New container for replies
        private UIImageView _authorAvatar;
        private List<Tuple<UILabel, UILabel>> _replyLabels = new List<Tuple<UILabel, UILabel>>(); // List to hold username and text labels for replies
        private bool _disposed_CommentCell = false;
        private CancellationTokenSource _commentCellCts;

        public event EventHandler<CommentItem> SeeRepliesTapped;

        public CommentCell(IntPtr handle)
            : base(handle)
        {
            _commentCellCts = new CancellationTokenSource();

            _authorAvatar = new UIImageView(); // Frame will be set in LayoutSubviews
            _authorAvatar.Layer.CornerRadius = 15;
            _authorAvatar.ClipsToBounds = true;
            _authorAvatar.BackgroundColor = UIColor.Gray;
            ContentView.AddSubview(_authorAvatar); // Add it to hierarchy ONCE

            _repliesContainerView = new UIView(); // Initialize container ONCE

            // Named handler for SeeRepliesTapped
            //_seeRepliesButton.TouchUpInside += OnSeeRepliesButtonTapped;

            _usernameLabel = new UILabel { Font = UIFont.FromName(VideoViewController.BoldFont, 14), TextColor = UIColor.Black };
            _dateLabel = new UILabel { Font = UIFont.FromName(VideoViewController.RegularFont, 12), TextColor = UIColor.LightGray, TextAlignment = UITextAlignment.Right };
            _commentTextLabel = new UILabel { Font = UIFont.FromName(VideoViewController.RegularFont, 14), Lines = 0, LineBreakMode = UILineBreakMode.WordWrap, TextColor = UIColor.Black };
            _likeCountLabel = new UILabel { Font = UIFont.FromName(VideoViewController.RegularFont, 12), TextColor = UIColor.Gray };
            _seeRepliesButton = new UIButton(UIButtonType.System);
            _seeRepliesButton.SetTitleColor(UIColor.LightGray, UIControlState.Normal);
            _seeRepliesButton.Font = UIFont.FromName(VideoViewController.RegularFont, 12);
           // _seeRepliesButton.TouchUpInside += (sender, e) =>
           // {
           //     if (SeeRepliesTapped != null)
           //     {
            //        SeeRepliesTapped(this, _comment);
            //    }
            //};

            _repliesContainerView = new UIView(); // Initialize the replies container

            ContentView.AddSubviews(_usernameLabel, _dateLabel, _commentTextLabel, _likeCountLabel, _seeRepliesButton, _repliesContainerView);
        }

        private void OnSeeRepliesButtonTapped(object sender, EventArgs e)
        {
            SeeRepliesTapped.Invoke(this, _comment); // Use null conditional operator if your C# version supports it
            // If not, use: if (SeeRepliesTapped != null) { SeeRepliesTapped(this, _comment); }
        }

        public void UpdateCell(CommentItem comment)
        {
            _comment = comment;
            _usernameLabel.Text = comment.Username;
            _dateLabel.Text = comment.Date;
            _commentTextLabel.Text = comment.Text;
            _likeCountLabel.Text = string.Format("{0} Likes", comment.LikeCount);
            _seeRepliesButton.Hidden = comment.Replies.Count == 0;
            _seeRepliesButton.SetTitle(comment.ShowReplies ? "Hide replies" : string.Format("See {0} replies", comment.Replies.Count), UIControlState.Normal);
            _authorAvatar.Image = null;

            try
            {
                Atnik.Tiktok.SetImage((NSData data) =>
                {
                    // Ensure UI update on main thread
                    InvokeOnMainThread(() =>
                    {
                        try
                        {
                            if (_commentCellCts.IsCancellationRequested) return; // Don't update if cancelled
                            if (_authorAvatar != null && _authorAvatar.Handle != IntPtr.Zero)
                            {
                                _authorAvatar.Image = UIImage.LoadFromData(data);
                            }
                            if (data != null) { data.Dispose(); }
                        }
                        catch { }
                    });
                }, comment.AvatarUrl, true, _commentCellCts);
            }
            catch { }

            // Clear old reply labels before adding new ones
            foreach (var labelPair in _replyLabels)
            {
                labelPair.Item1.RemoveFromSuperview();
                labelPair.Item1.Dispose(); // Dispose the native UILabel
                labelPair.Item2.RemoveFromSuperview();
                labelPair.Item2.Dispose(); // Dispose the native UILabel
            }
            _replyLabels.Clear();

            // Create and add reply labels if replies are to be shown
            if (comment.ShowReplies)
            {
                foreach (var reply in comment.Replies)
                {
                    var replyUsernameLabel = new UILabel
                    {
                        Text = string.Format("@{0}", reply.Username),
                        Font = UIFont.BoldSystemFontOfSize(12),
                        TextColor = UIColor.Black,
                    };
                    var replyTextLabel = new UILabel
                    {
                        Text = reply.Text,
                        Font = UIFont.SystemFontOfSize(12),
                        Lines = 0,
                        LineBreakMode = UILineBreakMode.WordWrap,
                        TextColor = UIColor.DarkGray,
                    };

                    _repliesContainerView.AddSubviews(replyUsernameLabel, replyTextLabel);
                    _replyLabels.Add(new Tuple<UILabel, UILabel>(replyUsernameLabel, replyTextLabel));
                }
            }

            LayoutSubviews();
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var padding = (float)16;
            var dateWidth = (float)50;
            var buttonWidth = _seeRepliesButton.Hidden ? (float)0 : (float)100;
            var paddingWithAvatar = padding + 40;

            _authorAvatar.Frame = new RectangleF(padding, padding, 30, 30);

            _usernameLabel.Frame = new RectangleF(paddingWithAvatar, padding, ContentView.Bounds.Width - paddingWithAvatar * 2 - dateWidth, 20);
            _dateLabel.Frame = new RectangleF(ContentView.Bounds.Width - dateWidth - padding, padding, dateWidth, 20);
            _commentTextLabel.Frame = new RectangleF(paddingWithAvatar, _usernameLabel.Frame.Bottom + 4, ContentView.Bounds.Width - paddingWithAvatar * 2, GetCommentTextHeight(_commentTextLabel.Text, ContentView.Bounds.Width - padding * 2, _commentTextLabel.Font));

            var currentY = _commentTextLabel.Frame.Bottom + 4;

            _likeCountLabel.Frame = new RectangleF(padding, currentY, 100, 20);
            _seeRepliesButton.Frame = new RectangleF(ContentView.Bounds.Width - buttonWidth - padding, currentY, buttonWidth, 20);

            // Position and size the replies container view
            var replyPadding = (float)8;
            currentY = _likeCountLabel.Frame.Bottom + 4;

            var repliesHeight = (float)0;
            foreach (var labelPair in _replyLabels)
            {
                var usernameHeight = GetCommentTextHeight(labelPair.Item1.Text, ContentView.Bounds.Width - padding * 3 - replyPadding, labelPair.Item1.Font);
                var textHeight = GetCommentTextHeight(labelPair.Item2.Text, ContentView.Bounds.Width - padding * 3 - replyPadding, labelPair.Item2.Font);
                repliesHeight += usernameHeight + 4 + textHeight + 4; // Username + padding + text + padding
            }

            _repliesContainerView.Frame = new RectangleF(padding + replyPadding, currentY, ContentView.Bounds.Width - padding * 2 - replyPadding, repliesHeight);

            // Layout the individual reply labels inside the container
            var replyY = (float)0;
            foreach (var labelPair in _replyLabels)
            {
                var replyUsernameLabel = labelPair.Item1;
                var replyTextLabel = labelPair.Item2;

                var usernameWidth = GetCommentTextHeight(replyUsernameLabel.Text, _repliesContainerView.Bounds.Width, replyUsernameLabel.Font);
                replyUsernameLabel.Frame = new RectangleF(0, replyY, usernameWidth, 20);

                replyTextLabel.Frame = new RectangleF(0, replyUsernameLabel.Frame.Bottom + 4, _repliesContainerView.Bounds.Width, GetCommentTextHeight(replyTextLabel.Text, _repliesContainerView.Bounds.Width, replyTextLabel.Font));

                replyY = replyTextLabel.Frame.Bottom + 4;
            }
        }

        public static float GetCommentTextHeight(string text, float width, UIFont font)
        {
            var tempLabel = new UILabel
            {
                Text = text,
                Font = font,
                Lines = 0,
                LineBreakMode = UILineBreakMode.WordWrap
            };
            var size = tempLabel.SizeThatFits(new SizeF(width, float.MaxValue));
            return (float)size.Height;
        }

        public static float GetCellHeight(CommentItem comment, float width)
        {
            // Use SizeThatFits for a more robust text size calculation
            var tempLabel = new UILabel
            {
                Text = comment.Text ?? string.Empty,
                Font = UIFont.SystemFontOfSize(14),
                Lines = 0,
                LineBreakMode = UILineBreakMode.WordWrap
            };
            var textHeight = tempLabel.SizeThatFits(new SizeF(width - 32, float.MaxValue)).Height;

            var baseHeight = (float)(16 + 20 + 4 + textHeight + 4 + 20 + 4); // Padding, username, padding, text, padding, likes/replies, padding

            if (comment.ShowReplies && comment.Replies.Count > 0)
            {
                var repliesHeight = (float)0;
                foreach (var reply in comment.Replies)
                {
                    repliesHeight += 16 + 20 + 4 + GetCommentTextHeight(reply.Text, width - 32, UIFont.SystemFontOfSize(12)) + 4;
                }
                return baseHeight + repliesHeight;
            }

            return baseHeight;
        }

        public void CleanupCommentCellForReuse()
        {
            // 1. Unsubscribe from events
            SeeRepliesTapped = null; // Clear the event delegate for this cell
            if (_seeRepliesButton != null) { _seeRepliesButton.TouchUpInside -= OnSeeRepliesButtonTapped; }
            // Add any other button event unsubscribes if applicable

            // 2. Null out UIImage references on UIImageViews
            if (_authorAvatar != null)
            {
                _authorAvatar.Image = null; // Release native image data
            }

            if (_commentCellCts != null)
            {
                _commentCellCts.Cancel();
                _commentCellCts.Dispose();
                _commentCellCts = null;
            }

            // 3. Dispose any remaining reply labels (safeguard)
            foreach (var labelPair in _replyLabels)
            {
                labelPair.Item1.RemoveFromSuperview();
                labelPair.Item1.Dispose();
                labelPair.Item2.RemoveFromSuperview();
                labelPair.Item2.Dispose();
            }
            _replyLabels.Clear();

            // 4. Clear text and hide elements for reuse
            _usernameLabel.Text = "";
            _dateLabel.Text = "";
            _commentTextLabel.Text = "";
            _likeCountLabel.Text = "";
            _seeRepliesButton.SetTitle("", UIControlState.Normal);
            _repliesContainerView.Hidden = true;
            // Hide or reset any other UI elements as needed
        }

        // PrepareForReuse should also call CleanupCommentCellForReuse
        public override void PrepareForReuse()
        {
            base.PrepareForReuse();
            CleanupCommentCellForReuse();
        }

        // Dispose override for CommentCell
        protected override void Dispose(bool disposing)
        {
            if (!_disposed_CommentCell)
            {
                if (disposing)
                {
                    // Ensure cleanup for reuse is done
                    CleanupCommentCellForReuse();

                    // Any other managed IDisposable instance fields not handled by CleanupCommentCellForReuse()
                    // (e.g., if you added a CancellationTokenSource to CommentCell for async operations within it)

                    // For UIViews (_usernameLabel, _dateLabel, _repliesContainerView etc.),
                    // base.Dispose handles the release of their native resources as they are subviews.
                    // You only need to explicitly dispose if you created them AND kept strong references
                    // to them OUTSIDE the view hierarchy.
                }
                _disposed_CommentCell = true; // Mark as disposed
            }
            base.Dispose(disposing); // Always call base.Dispose to release native resources
        }
    }

    // Data source for the comments table view
    public class CommentsDataSource : UITableViewDataSource
    {
        private readonly List<CommentItem> _comments;
        private readonly UITableView _tableView;
        private static readonly NSString CellId = new NSString("CommentCell");

        public CommentsDataSource(List<CommentItem> comments, UITableView tableView)
        {
            _comments = comments;
            _tableView = tableView;
        }

        public override int RowsInSection(UITableView tableView, int section)
        {
            return _comments.Count;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            // Using NSString for the identifier to avoid potential native interop issues
            var cell = (CommentCell)tableView.DequeueReusableCell(CellId, indexPath);
            var comment = _comments[indexPath.Row];
            cell.UpdateCell(comment);
            cell.SeeRepliesTapped += OnSeeRepliesTapped;
            return cell;
        }

        private void OnSeeRepliesTapped(object sender, CommentItem comment)
        {
            comment.ShowReplies = !comment.ShowReplies;
            _tableView.ReloadData();
        }
    }

    // Delegate for the comments table view to handle row heights
    public class CommentsDelegate : UITableViewDelegate
    {
        private readonly List<CommentItem> _comments;
        private readonly UITableView _tableView;

        public CommentsDelegate(List<CommentItem> comments, UITableView tableView)
        {
            _comments = comments;
            _tableView = tableView;
        }

        public override float GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
        {
            var comment = _comments[indexPath.Row];
            return (float)CommentCell.GetCellHeight(comment, (float)tableView.Bounds.Width);
        }
    }
}
