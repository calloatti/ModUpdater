using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Steamworks;

namespace ModUpdater
{
    public class ModComparison
    {
        public PublishedFileId_t Id;
        public string Name;
        public uint LocalUnix;
        public uint RemoteUnix;
        public string Status;
    }

    class Program
    {
        private const string MenuInstructions = "1:List | 2:Update Pending | 3:Force All | Q:Quit";

        private static CallResult<SteamUGCQueryCompleted_t> m_UGCQueryCompleted;
        private static Callback<DownloadItemResult_t> m_DownloadItemResult;

        private static List<ModComparison> modDataList = new List<ModComparison>();
        private static Queue<PublishedFileId_t> downloadQueue = new Queue<PublishedFileId_t>();
        private static PublishedFileId_t currentDownloadingId = PublishedFileId_t.Invalid;
        private static bool isWaitingForCallback = false;

        // Tracks if we need to auto-run an update after the list is fetched
        private static int nextStepAfterList = 0; // 0: None, 2: Smart Update, 3: Force All

        static void Main(string[] args)
        {
            Console.Title = "ModUpdater";

            if (!SteamAPI.Init())
            {
                Console.WriteLine("SteamAPI_Init failed. Ensure steam_appid.txt is present.");
                return;
            }

            m_UGCQueryCompleted = CallResult<SteamUGCQueryCompleted_t>.Create(OnUGCQueryCompleted);
            m_DownloadItemResult = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);

            Console.WriteLine(MenuInstructions);

            while (true)
            {
                SteamAPI.RunCallbacks();

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1)
                    {
                        nextStepAfterList = 0;
                        StartComparisonRequest();
                    }
                    else if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2)
                    {
                        // If we haven't listed yet, do it first
                        if (modDataList.Count == 0) { nextStepAfterList = 2; StartComparisonRequest(); }
                        else PrepareUpdateQueue(false);
                    }
                    else if (key == ConsoleKey.D3 || key == ConsoleKey.NumPad3)
                    {
                        // If we haven't listed yet, do it first
                        if (modDataList.Count == 0) { nextStepAfterList = 3; StartComparisonRequest(); }
                        else PrepareUpdateQueue(true);
                    }
                    else if (key == ConsoleKey.Q) break;
                }

                ProcessQueue();
                Thread.Sleep(200);
            }

            SteamAPI.Shutdown();
        }

        static void StartComparisonRequest()
        {
            uint count = SteamUGC.GetNumSubscribedItems();
            if (count == 0) return;
            PublishedFileId_t[] ids = new PublishedFileId_t[count];
            SteamUGC.GetSubscribedItems(ids, count);

            modDataList.Clear();
            foreach (var id in ids)
            {
                uint state = SteamUGC.GetItemState(id);
                ulong size; string path; uint localTime;
                bool inst = SteamUGC.GetItemInstallInfo(id, out size, out path, 1024, out localTime);
                modDataList.Add(new ModComparison { Id = id, Name = "...", LocalUnix = inst ? localTime : 0, Status = GetStatusDescription((EItemState)state) });
            }

            //
            UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest(ids, count);
            m_UGCQueryCompleted.Set(SteamUGC.SendQueryUGCRequest(handle));
            Console.WriteLine("\nFetching current mod data from Steam...");
        }

        static void OnUGCQueryCompleted(SteamUGCQueryCompleted_t p, bool fail)
        {
            if (fail || p.m_eResult != EResult.k_EResultOK) return;

            for (uint i = 0; i < p.m_unNumResultsReturned; i++)
            {
                SteamUGCDetails_t det;
                if (SteamUGC.GetQueryUGCResult(p.m_handle, i, out det))
                {
                    var m = modDataList.Find(x => x.Id == det.m_nPublishedFileId);
                    if (m != null) { m.RemoteUnix = det.m_rtimeUpdated; m.Name = det.m_rgchTitle; }
                }
            }

            modDataList.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));

            // Print the table so the user sees what's happening
            PrintTable();

            // Auto-trigger the next step if requested
            if (nextStepAfterList == 2) { nextStepAfterList = 0; PrepareUpdateQueue(false); }
            else if (nextStepAfterList == 3) { nextStepAfterList = 0; PrepareUpdateQueue(true); }

            SteamUGC.ReleaseQueryUGCRequest(p.m_handle);
        }

        static void PrepareUpdateQueue(bool forceAll)
        {
            if (isWaitingForCallback) return;
            downloadQueue.Clear();

            foreach (var m in modDataList)
            {
                bool shouldAdd = forceAll;
                if (!forceAll && (m.RemoteUnix > m.LocalUnix || m.Status == "UpdateReq")) shouldAdd = true;
                if (shouldAdd) downloadQueue.Enqueue(m.Id);
            }

            if (downloadQueue.Count > 0) Console.WriteLine($"\nQueueing {downloadQueue.Count} mods...");
            else { Console.WriteLine("\nAll mods up to date."); Console.WriteLine(MenuInstructions); }
        }

        static void ProcessQueue()
        {
            if (!isWaitingForCallback && downloadQueue.Count > 0)
            {
                Thread.Sleep(1000); //
                currentDownloadingId = downloadQueue.Dequeue();
                if (SteamUGC.DownloadItem(currentDownloadingId, true)) isWaitingForCallback = true;
            }

            if (isWaitingForCallback && currentDownloadingId != PublishedFileId_t.Invalid)
            {
                ulong d, t;
                if (SteamUGC.GetItemDownloadInfo(currentDownloadingId, out d, out t) && t > 0)
                    Console.Write($"\rProgress: {(double)d / t * 100:F1}% ({d / 1024 / 1024}MB/{t / 1024 / 1024}MB)    ");
                else Console.Write($"\rVerifying {currentDownloadingId}...           ");
            }
        }

        static void OnDownloadItemResult(DownloadItemResult_t p)
        {
            if (p.m_nPublishedFileId == currentDownloadingId)
            {
                EItemState state = (EItemState)SteamUGC.GetItemState(p.m_nPublishedFileId);
                if ((state & EItemState.k_EItemStateDownloading) != 0) return;

                isWaitingForCallback = false;
                currentDownloadingId = PublishedFileId_t.Invalid;

                if (downloadQueue.Count == 0) { Console.WriteLine("\nBatch complete."); Console.WriteLine(MenuInstructions); }
            }
        }

        static void PrintTable()
        {
            Console.WriteLine("\nID         Status       Local Date       Remote Date      Name");
            Console.WriteLine(new string('-', 85));
            foreach (var mod in modDataList)
            {
                string line = $"{mod.Id.ToString().PadRight(10)} {mod.Status.PadRight(12)} {FormatUnix(mod.LocalUnix).PadRight(16)} {FormatUnix(mod.RemoteUnix).PadRight(16)} {mod.Name}";
                if (mod.RemoteUnix > mod.LocalUnix && mod.LocalUnix != 0) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine(line + " [!] "); Console.ResetColor(); }
                else Console.WriteLine(line);
            }
            Console.WriteLine(new string('-', 85) + "\n" + MenuInstructions);
        }

        static string FormatUnix(uint t) => t == 0 ? "N/A" : new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(t).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        static string GetStatusDescription(EItemState s)
        {
            if ((s & EItemState.k_EItemStateNeedsUpdate) != 0) return "UpdateReq";
            if ((s & EItemState.k_EItemStateDownloading) != 0) return "Downloading";
            if ((s & EItemState.k_EItemStateInstalled) != 0) return "Installed";
            return "Subscribed";
        }
    }
}