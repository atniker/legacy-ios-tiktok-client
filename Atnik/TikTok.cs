using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoTouch.Foundation;
using Plugin.SecureStorage;
using System.Threading.Tasks;
using System.Net;
using TikTok.Views;
using System.Threading;
using MonoTouch.UIKit;

namespace TikTok.Atnik
{
    class Tiktok
    {
        public static Dictionary<string, NSData> cachedPictures = new Dictionary<string, NSData>();
        public static string address = "http://miriznik-33543.portmap.host:33543/";
        public static string currentSession 
        {
            get {
                return UserPreferences.GetString("session");
            }
            set {
                UserPreferences.SetString("session", value);
            }
        }

        public static string MsToken;
        public static Views.Profile.Profile myProfile;

        public static void Init()
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                return true;
            };
        }

        async public static Task<Dictionary<string, object>> MakeRequest(string endpoint, string data=null, CancellationTokenSource cts=null, bool check_auth=true) 
        {
            var uri = new Uri(address + endpoint);
            var client = new WebClient();
            client.Headers[HttpRequestHeader.ContentType] = "application/json";

            if (cts != null)
            {
                cts.Token.Register(client.CancelAsync);
            }

            try
            {
                return MiniJSON.Json.Deserialize(data == null ? await client.DownloadStringTaskAsync(uri) : await client.UploadStringTaskAsync(uri, "post", data)) as Dictionary<string, object>;
            }
            catch (WebException ex)
            {
                if (check_auth)
                {
                    var r = ex.Response as HttpWebResponse;

                    if (r != null)
                    {
                        if (r.StatusCode == HttpStatusCode.Forbidden)
                        {
                            VideoViewController.instance.InvokeAsMain(() =>
                            {
                                var alert = new UIAlertView("Error", "your session has expired. You'll be directed to the setup screen", null, "ok");
                                alert.Clicked += (object s, UIButtonEventArgs e) =>
                                {
                                    Environment.Exit(0);
                                    //AppDelegate.instance.RestartApplicationUI();
                                };
                                alert.Show();
                                return;
                            });
                        }
                    }
                }

                throw ex;
            }

            return null;
            
        }

        public static Views.Profile.Profile MakeProfile(Dictionary<string, object> resultProfile)
        {
            var resultProfileVideos = new List<Views.Profile.VideoItem>();

            foreach (var i in (List<object>)resultProfile["videos"]) 
            {
                var data = (Dictionary<string, object>)i;
                resultProfileVideos.Add(new Views.Profile.VideoItem(Convert.ToInt32(data["is_pinned"]) == 1, (string)data["preview_url"], (string)data["view_count"], (string)data["video_url"]));
            }

            var p = new Views.Profile.Profile() 
            {
                AvatarUrl = (string)resultProfile["avatar_url"],
                Name = (string)resultProfile["name"],
                Username = (string)resultProfile["username"],
                FollowerCount = (string)resultProfile["followers_count"],
                FollowingCount = (string)resultProfile["following_count"],
                HeartCount = (string)resultProfile["like_count"],
                Description = (string)resultProfile["description"],
                Videos = resultProfileVideos.ToArray()
            };

            return p;
        }

        async public static Task AcquireSession() 
        {
            Dictionary<string, object> result = null;

            if (currentSession != null) 
            {
                try
                {
                    result = await MakeRequest("validate_session/" + currentSession, check_auth: false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }

            if (result == null)
            {
                result = await MakeRequest("get_session", data: MiniJSON.Json.Serialize(new Dictionary<string, string>() { { "cookies", MsToken } }));
            }

            var downloadData = (Dictionary<string, object>)result["download_data"];

            myProfile = MakeProfile((Dictionary<string, object>)result["profile"]);
            Warmboot.downloadHeaders = (Dictionary<string, object>)downloadData["headers"];
            Warmboot.downloadCookies = (string)downloadData["cookies"];

            currentSession = (string)result["result"];
        }

        async public static Task<Dictionary<string, object>> GetVideo(string author, string id) 
        {
            return await MakeRequest(string.Format("get_video/{0}/{1}/{2}", author, id, currentSession));
        }

        async public static Task<List<VideoItem>> GetForYouPage()
        {
            List<VideoItem> result = new List<VideoItem>();
            var data = await MakeRequest("get_trending/" + currentSession);

            foreach (var i in (List<object>)data["result"])
            {
                var video = (Dictionary<string, object>)i;
                var videoAuthor = (Dictionary<string, object>)video["author"];
                var videoVideo = (Dictionary<string, object>)video["video"];

                result.Add(new VideoItem()
                {
                    Author = new Author() {
                        Avatar = (string)videoAuthor["avatar"],
                        Username = (string)videoAuthor["username"]
                    },
                    CommentCount = (string)(videoVideo["commentCount"]),
                    Description = (string)videoVideo["desc"],
                    Hearts = (string)(videoVideo["heartCount"]),
                    Id = (string)videoVideo["id"],
                    VideoUrl = (string)videoVideo["video_url"]
                });
            }

            return result;
        }

        async public static Task<List<CommentItem>> GetComments(string author, string id)
        {
            List<CommentItem> result = new List<CommentItem>();
            var data = await MakeRequest(string.Format("get_comments/{0}/{1}/{2}", author, id, currentSession));

            foreach (var i in (List<object>)data["result"])
            {
                var comment = (Dictionary<string, object>)i;
                var commentAuthor = (Dictionary<string, object>)comment["author"];

                result.Add(new CommentItem()
                {
                    AvatarUrl = (string)commentAuthor["avatar_url"],
                    ProfileUrl = (string)commentAuthor["profile_link"],
                    Date = (string)comment["date"],
                    Text = (string)comment["content"],
                    Username = (string)commentAuthor["username"],
                    LikeCount = (string)comment["likes"],
                    ShowReplies = false,
                    Replies = new List<CommentItem>()
                });
            }

            return result;
        }

        async public static Task<Views.Profile.Profile> GetProfile(string author, CancellationTokenSource cts)
        {
            var request = await MakeRequest(string.Format("get_profile/{0}/{1}", author, currentSession), cts: cts);

            return MakeProfile((Dictionary<string, object>)request["result"]);
        }

        async public static Task<List<Views.Notifications.Notification>> GetNotifications(CancellationTokenSource cts)
        {
            var result = new List<Views.Notifications.Notification>();
            var request = await MakeRequest(string.Format("get_notifications/{0}", currentSession), cts: cts);

            foreach (var i in (List<object>)request["result"]) 
            {
                var data = (Dictionary<string, object>)i;
                result.Add(new Views.Notifications.Notification((string)data["avatar"], (string)data["title"], (string)data["description"], (string)data["profile_url"]));
            }

            return result;
        }

        public static void Debug(string content)
        {
            VideoViewController.instance.InvokeAsMain(() =>
            {
                new UIAlertView("", content, null, "oc");
            });
        }

        // In Atnik.Tiktok (assumed class)
        public static void SetImage(Action<NSData> callback, string url, bool assisted = false)
        {
            if (cachedPictures.ContainsKey(url))
            {
                VideoViewController.instance.InvokeAsMain(() => { callback.Invoke(cachedPictures[url]); });
            }

            var request = new WebClient();

            if (!assisted)
            {
                request.DownloadDataAsync(new Uri(url.Replace("https://", "http://")));

                request.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs args) =>
                {
                    if (args.Error != null)
                    {
                        Console.WriteLine(args.Error);
                        return;
                    }

                    var data = NSData.FromArray(args.Result);

                    while (cachedPictures.Count > 100)
                    {
                        cachedPictures.Remove(cachedPictures.Keys.First());
                    }

                    cachedPictures[url] = data;
                    VideoViewController.instance.InvokeAsMain(() => { callback.Invoke(data); });
                };
            }
            else
            {
                request.Headers[HttpRequestHeader.ContentType] = "application/json";
                request.UploadStringAsync(new Uri(address + "get_avatar/" + currentSession), MiniJSON.Json.Serialize(new Dictionary<string, string>() { { "url", url } }));

                request.UploadStringCompleted += (object sender, UploadStringCompletedEventArgs args) =>
                {
                    if (args.Error != null)
                    {
                        Console.WriteLine(args.Error);
                        return;
                    }

                    var data = NSData.FromArray(Convert.FromBase64String(args.Result));

                    while (cachedPictures.Count > 100)
                    {
                        cachedPictures.Remove(cachedPictures.Keys.First());
                    }

                    cachedPictures[url] = data;
                    VideoViewController.instance.InvokeAsMain(() => { callback.Invoke(data); });
                };
            }

        }

        public static void SetImage(Action<NSData> callback, string url, bool assisted = false, CancellationTokenSource cancellationToken = null)
        {
            if (cachedPictures.ContainsKey(url))
            {
                VideoViewController.instance.InvokeAsMain(() => { callback.Invoke(cachedPictures[url]); });
            }

            var request = new WebClient();

            if (!assisted)
            {
                request.DownloadDataAsync(new Uri(url.Replace("https://", "http://")));

                request.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs args) =>
                {
                    if (args.Error != null)
                    {
                        Console.WriteLine(args.Error);
                        return;
                    }

                    var data = NSData.FromArray(args.Result);

                    while (cachedPictures.Count > 100)
                    {
                        string key = cachedPictures.Keys.First();

                        try
                        {
                            cachedPictures[key].Dispose();
                        }
                        catch { }

                        cachedPictures.Remove(key);
                    }

                    cachedPictures[url] = data;
                    VideoViewController.instance.InvokeAsMain(() => { callback.Invoke(data); });
                };
            }
            else
            {
                request.Headers[HttpRequestHeader.ContentType] = "application/json";
                request.UploadStringAsync(new Uri(address + "get_avatar/" + currentSession), MiniJSON.Json.Serialize(new Dictionary<string, string>() { { "url", url } }));

                request.UploadStringCompleted += (object sender, UploadStringCompletedEventArgs args) =>
                {
                    if (args.Error != null)
                    {
                        Console.WriteLine(args.Error);
                        return;
                    }

                    var data = NSData.FromArray(Convert.FromBase64String(args.Result));

                    while (cachedPictures.Count > 100)
                    {
                        cachedPictures.Remove(cachedPictures.Keys.First());
                    }

                    cachedPictures[url] = data;
                    VideoViewController.instance.InvokeAsMain(() => { callback.Invoke(data); });
                };
            }
        }
    }
}
