using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ReelBox
{
    public class MainModel : INotifyPropertyChanged
    {
        private ObservableCollection<Medium> _media;
        public ObservableCollection<Medium> Media
        {
            get => _media;
            set
            {
                if (SetProperty(ref _media, value, alsoNotify: [nameof(HasMedia)]))
                {
                    _media.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMedia));
                }
            }
        }
        private bool _incompactmode;
        public bool InCompactMode
        {
            get => _incompactmode;
            set => SetProperty(ref _incompactmode, value);
        }
        private bool _hasselected;
        public bool HasSelected
        {
            get => _hasselected;
            set => SetProperty(ref _hasselected, value);
        }
        private bool _allareselected;
        public bool AllAreSelected
        {
            get => _allareselected;
            set => SetProperty(ref _allareselected, value);
        }

        public bool HasMedia => Media.Count > 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            foreach (var dep in alsoNotify) OnPropertyChanged(dep);
            return true;
        }
    }

    public class Medium : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        private MediaDetails _details;
        public MediaDetails Details
        {
            get => _details;
            set => SetProperty(ref _details, value);
        }
        public MediaType MediaType { get; set; }
        private Action[] _availableactions;
        public Action[] AvailableActions
        {
            get => _availableactions;
            set
            {
                ActionModels = value.Select(a =>
                {
                    var (icon, text) = ActionIconAndText(a);
                    return new ActionModel{ Owner = this, Icon = icon, Text = text };
                }).ToArray();
                SetProperty(ref _availableactions, value, alsoNotify:nameof(ActionModels));
            }
        }
        private bool _isselected;
        public bool IsSelected
        {
            get => _isselected;
            set => SetProperty(ref _isselected, value);
        }
        public string Icon => MediaType switch
        {
            MediaType.Video => "\uE714",
            MediaType.Audio => "\uE8D6",
            MediaType.Image => "\uEB9F",
            MediaType.Subtitle => "\uED1E",
            _ => throw new ArgumentOutOfRangeException()
        };
        public ActionModel[] ActionModels { get; set; }

        private (string icon, string text) ActionIconAndText(Action action)
        {
            return action switch
            {
                Action.Split => ("\uE78A", "Split"),
                Action.Merge => ("\uF5A9", "Merge"),
                Action.Crop => ("\uE7A8", "Crop"),
                Action.Compress => ("\uE73F", "Compress"),
                Action.Tour => ("\uF57D", "Tour"),
                Action.Mix => ("\uE81E", "Mix"),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            foreach (var dep in alsoNotify) OnPropertyChanged(dep);
            return true;
        }
    }

    public class ActionModel
    {
        public Medium Owner { get; set; }
        public string Icon { get; set; }
        public string Text { get; set; }
    }

    public class MediaDetails
    {
        public double FileSize { get; set; }
        public string? Resolution { get; set; }
        public string? Duration { get; set; }
        public string? Bitrate { get; set; }
        public string? SampleRate { get; set; }
        public string? FPS { get; set; }
        public string? ThumbnailPath { get; set; }
        public int VideoCount { get; set; }
        public int AudioCount { get; set; }
        public int SubtitleCount { get; set; }
        public int AttachmentCount { get; set; }
        public int ChapterCount { get; set; }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Resolution != null) parts.Add(Resolution);
            if (Duration != null) parts.Add(Duration);
            if (Bitrate != null) parts.Add(Bitrate);
            if (FPS != null) parts.Add(FPS);
            if (SampleRate != null) parts.Add(SampleRate);
            switch (FileSize)
            {
                case >= 1024 * 1024 * 1024:
                    parts.Add($"{FileSize / (1024 * 1024 * 1024):0.##} GB");
                    break;
                case >= 1024 * 1024:
                    parts.Add($"{FileSize / (1024 * 1024):0.##} MB");
                    break;
                default:
                    parts.Add($"{FileSize / 1024:0.##} KB");
                    break;
            }
            if (VideoCount + AudioCount + SubtitleCount + ChapterCount > 1)
            {
                var streams = new List<string>();
                if (VideoCount > 0) streams.Add($"{VideoCount} video{(VideoCount > 1 ? "s" : "")}");
                if (AudioCount > 0) streams.Add($"{AudioCount} audio{(AudioCount > 1 ? "s" : "")}");
                if (SubtitleCount > 0) streams.Add($"{SubtitleCount} subtitle{(SubtitleCount > 1 ? "s" : "")}");
                if (AttachmentCount > 0) streams.Add($"{AttachmentCount} attachment{(AttachmentCount > 1 ? "s" : "")}");
                if (ChapterCount > 0) streams.Add($"{ChapterCount} chapter{(ChapterCount > 1 ? "s" : "")}");
                parts.Add(string.Join(" • ", streams));
            }
            return string.Join(" • ", parts);
        }
    }

    public enum MediaType
    {
        Video,
        Audio,
        Image,
        Subtitle
    }

    public enum Action
    {
        Split,
        Merge,
        Crop,
        Compress,
        Mix,
        Tour
    }
}
