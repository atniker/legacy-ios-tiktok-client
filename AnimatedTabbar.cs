using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace TikTok
{
    public class AnimatedTabbar : UITabBarControllerDelegate
    {
        public override bool ShouldSelectViewController(UITabBarController tabBarController, UIViewController viewController)
        {
            UIViewController fromViewController = tabBarController.SelectedViewController;

            if (fromViewController == null || fromViewController == viewController)
            {
                return true;
            }

            UIView fromView = fromViewController.View;
            UIView toView = viewController.View;
            toView.Frame = fromView.Frame;

            if (toView.Superview == null)
            {
                fromView.Superview.AddSubview(toView);
            }

            try
            {
                fromView.Superview.BringSubviewToFront(toView);
            }
            catch
            {
                return false;
            }
            
            float animationDuration = 0.2f;

            UIView.Transition(fromView, toView, animationDuration, UIViewAnimationOptions.TransitionCrossDissolve, () =>
            {
                fromView.RemoveFromSuperview();
                tabBarController.SelectedViewController = viewController;
            });

            return false;
        }
    }
}
