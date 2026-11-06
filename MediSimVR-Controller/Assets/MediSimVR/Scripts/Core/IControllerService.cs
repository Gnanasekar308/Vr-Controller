using System;
using System.Collections.Generic;

namespace MediSimVR
{
    // Everything the UI needs from "the Quest side". Today: MockControllerService (UI only).
    // Later: a real implementation that talks to the co-worker's Quest Receiver; the UI does not change.
    public interface IControllerService
    {
        event Action Changed;

        IReadOnlyList<QuestDevice> Devices { get; }
        IReadOnlyList<MediaFile> Files { get; }
        IReadOnlyList<AppNotification> Notifications { get; }

        bool Scanning { get; }
        string ControlFileId { get; set; }
        bool NotificationsEnabled { get; set; }
        int UnreadCount { get; }

        MediaFile ControlFile { get; }
        List<QuestDevice> SelectedOnline();
        void Refresh();
        void Connect(List<QuestDevice> devices);
        void AddManualDevice(string ip);

        void AddFile(MediaFile f);
        void UpdateFile(MediaFile f);
        void RemoveFile(MediaFile f);
        void Send(MediaFile f);          // send to the selected Quests and start it there

        void Play();
        void Pause();
        void Resume();
        void Stop();
        void SetMute(bool mute);
        void ShowText();
        void HideText();

        void MarkAllRead();
        void ClearNotifications();
        void Tick(float dt);
    }
}
