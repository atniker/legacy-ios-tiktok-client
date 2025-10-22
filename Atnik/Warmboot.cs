using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace TikTok.Atnik
{
    public class WarmbootItem
    {
        public string url { get; set; }
        public string itemId { get; set; }
    }

    public static class Warmboot
    {
        private static string documentsPath;
        public static Dictionary<string, object> downloadHeaders;
        public static string downloadCookies;

        private static ConcurrentDictionary<string, TaskCompletionSource<bool>> videoReadySources =
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        static Warmboot()
        {
            documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public static string GetItemPath(string id)
        {
            return Path.Combine(documentsPath, id + ".mp4");
        }

        async public static void BufferVideos(Dictionary<string, Func<Task>> downloadActions)
        {
            foreach (var kvp in downloadActions)
            {
                string itemId = kvp.Key;
                Func<Task> downloadAction = kvp.Value;

                System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Initiating download for {0}", itemId));

                try
                {
                    if (videoReadySources.ContainsKey(itemId))
                    {
                        await downloadAction.Invoke();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Skipping download for {0} as it's no longer in queue.", itemId));
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Warmboot Error: BufferVideos failed for {0}: {1}", itemId, ex));
                }
                finally
                {
                    TaskCompletionSource<bool> removedTcs;
                    if (videoReadySources.TryRemove(itemId, out removedTcs))
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Removed TCS for {0} after download attempt.", itemId));
                        // Call TrySetCanceled without arguments as per your TaskCompletionSource definition
                        removedTcs.TrySetCanceled();
                    }
                }
            }
        }

        async public static void LoadVideos(WarmbootItem[] videos)
        {
            var loadActions = new Dictionary<string, Func<Task>>();

            foreach (var i in videos)
            {
                // Removed TaskCreationOptions.RunContinuationsAsynchronously
                var tcs = new TaskCompletionSource<bool>();

                if (!videoReadySources.TryAdd(i.itemId, tcs))
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Skipping adding {0}, it's already in the queue or being handled.", i.itemId));
                    continue;
                }

                loadActions[i.itemId] = async () =>
                {
                    using (var client = new WebClient())
                    {
                        foreach (var j in downloadHeaders)
                        {
                            client.Headers.Add(j.Key, (string)j.Value);
                        }
                        client.Headers.Add("Cookie", downloadCookies);

                        try
                        {
                            await client.DownloadFileTaskAsync(new Uri(i.url.Replace("https://", "http://")), GetItemPath(i.itemId));
                            tcs.TrySetResult(true);
                            System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Downloaded {0}: {1}", i.itemId, i.url));
                        }
                        catch (OperationCanceledException)
                        {
                            // Call TrySetCanceled without arguments
                            tcs.TrySetCanceled();
                            System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Download for {0} cancelled.", i.itemId));
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                            System.Diagnostics.Debug.WriteLine(string.Format("Warmboot Error: Failed to download {0} ({1}): {2}", i.itemId, i.url, ex.Message));
                        }
                    }
                };
            }

            BufferVideos(loadActions);
        }

        public static void Cleanup(string[] itemIds)
        {
            foreach (var itemId in itemIds)
            {
                try
                {
                    string filePath = GetItemPath(itemId);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Deleted video file: {0}", filePath));
                    }

                    TaskCompletionSource<bool> removedTcs;
                    if (videoReadySources.TryRemove(itemId, out removedTcs))
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Removed TCS for {0} during cleanup.", itemId));
                        // Call TrySetCanceled without arguments
                        removedTcs.TrySetCanceled();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Warmboot Error: Cleanup for {0} failed: {1}", itemId, ex.Message));
                }
            }
        }

        public static void Cleanup()
        {
            foreach (var i in Directory.GetFiles(documentsPath))
            {
                try
                {
                    if (i.EndsWith(".mp4"))
                    {
                        File.Delete(i);
                    }
                }
                catch { }
                
            }
        }

        public static async Task WaitForVideoReadyAsync(string itemId, CancellationToken token)
        {
            TaskCompletionSource<bool> tcs;

            if (videoReadySources.TryGetValue(itemId, out tcs))
            {
                // CancellationTokenRegistration.Register(Action) accepts an Action, not Action<CancellationToken>.
                // The CancellationToken itself is not passed to TrySetCanceled() in older versions.
                using (token.Register(() => tcs.TrySetCanceled()))
                {
                    await tcs.Task;
                }
            }
            else
            {
                if (File.Exists(GetItemPath(itemId)))
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Video {0} found on disk, no TCS needed.", itemId));
                    return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Warmboot: Video {0} not found in warmboot queue or on disk.", itemId));
                    throw new InvalidOperationException(string.Format("Video {0} not found in warmboot queue or on disk.", itemId));
                }
            }
        }
    }
}