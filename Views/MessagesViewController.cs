using System;
using MonoTouch.UIKit;
using System.Drawing;
using System.Threading.Tasks;
using MonoTouch.Foundation;

namespace TikTok.Views
{
    public class MessagesViewController : UIViewController
    {
        UIColor transparency;
        UILabel welcomeLabel;

        const float buttonWidth = 200;
        const float buttonHeight = 50;
        const float padding = 10;

        public static bool failed;
        private string _label;

        public MessagesViewController(string label)
        {
            _label = label;
            Title = label;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            transparency = UIColor.Clear;

            View.Frame = UIScreen.MainScreen.Bounds;
            View.BackgroundColor = UIColor.White;
            View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            welcomeLabel = new UILabel(new RectangleF(
                padding,
                View.Frame.Height / 2 - 60,
                View.Frame.Width - padding * 2,
                30));
            welcomeLabel.Text = _label;
            welcomeLabel.Font = UIFont.FromName(VideoViewController.RegularFont, 14);
            welcomeLabel.TextAlignment = UITextAlignment.Center;
            welcomeLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            welcomeLabel.BackgroundColor = transparency;
            welcomeLabel.TextColor = UIColor.LightGray;
            View.AddSubview(welcomeLabel);
        }

        public void OnTabActivated(bool isFirstActivation)
        {

        }
    }
}
