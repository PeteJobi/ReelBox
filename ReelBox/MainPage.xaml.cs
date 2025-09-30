using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using CompressMediaPage;
using ConcatMediaPage;
using ImageTour;
using MediaTrackMixerPage;
using VideoCropper;
using VideoSplitter;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ReelBox
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainModel viewModel = new(){ Media = [] };
        private Processor processor;
        private string ffmpegPath;
        private Medium? currentActionMedium;

        public MainPage()
        {
            InitializeComponent();
        }

        private async Task AddMedia(string[] paths, int index = 0)
        {
            var insertAtEnd = index == 0;
            var newMedia = new List<Medium>();
            foreach (var path in paths)
            {
                var medium = Processor.GetMedium(path);
                if(medium == null) continue;
                newMedia.Add(medium);
                if (insertAtEnd) viewModel.Media.Add(medium);
                else viewModel.Media.Insert(index++, medium);
                medium.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(Medium.IsSelected)) SetSelectedBools();
                };
            }

            await Task.WhenAll(
                newMedia.Select(async m => m.Details = await processor.GetMediaDetails(m.FilePath, m.MediaType)));
        }

        private void SetSelectedBools()
        {
            viewModel.HasSelected = viewModel.Media.Any(m => m.IsSelected);
            viewModel.AllAreSelected = viewModel.Media.All(m => m.IsSelected);
        }

        private async void MainPage_OnDrop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                await AddMedia(items.Select(i => i.Path).ToArray());
            }
        }

        private void MainPage_OnDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void ShowFilePicker(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            Processor.AllSupportedTypes.ForEach(t => filePicker.FileTypeFilter.Add(t));
            var windowId = XamlRoot?.ContentIslandEnvironment?.AppWindowId;
            var hwnd = Win32Interop.GetWindowFromWindowId(windowId.Value);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
            var files = await filePicker.PickMultipleFilesAsync();
            await AddMedia(files.Select(f => f.Path).ToArray());
        }

        private void RemoveMedia(object sender, RoutedEventArgs e)
        {
            if(viewModel.HasSelected)
            {
                var selectedMedia = viewModel.Media.Where(m => m.IsSelected).ToArray();
                foreach (var medium in selectedMedia) viewModel.Media.Remove(medium);
            }
            else viewModel.Media.Clear();
            SetSelectedBools();
        }

        private void ActionClicked(object sender, RoutedEventArgs e)
        {
            var button = (AppBarButton)sender;
            var actionModel = (ActionModel)button.DataContext;
            currentActionMedium = actionModel.Owner;
            var mediaPath = actionModel.Owner.FilePath;
            var thisTypeName = typeof(MainPage).FullName;

            switch (actionModel.Action)
            {
                case Action.Split:
                    Frame.Navigate(typeof(VideoSplitter.VideoSplitterPage), new SplitterProps { FfmpegPath = ffmpegPath, VideoPath = mediaPath, TypeToNavigateTo = thisTypeName });
                    break;
                case Action.Merge:
                    Frame.Navigate(typeof(ConcatMediaPage.ConcatMediaPage), new ConcatProps { FfmpegPath = ffmpegPath, MediaPaths = [mediaPath], TypeToNavigateTo = thisTypeName });
                    break;
                case Action.Crop:
                    Frame.Navigate(typeof(VideoCropper.VideoCropperPage), new CropperProps { FfmpegPath = ffmpegPath, VideoPath = mediaPath, TypeToNavigateTo = thisTypeName });
                    break;
                case Action.Compress:
                    Frame.Navigate(typeof(CompressMediaPage.CompressMediaPage), new CompressProps { FfmpegPath = ffmpegPath, MediaPath = mediaPath, TypeToNavigateTo = thisTypeName });
                    break;
                case Action.Mix:
                    Frame.Navigate(typeof(MediaTrackMixerMainPage), new MixerProps { FfmpegPath = ffmpegPath, MediaPaths = [mediaPath], TypeToNavigateTo = thisTypeName });
                    break;
                case Action.Tour:
                    Frame.Navigate(typeof(ImageTour.ImageTourPage), new TourProps { FfmpegPath = ffmpegPath, MediaPath = mediaPath, TypeToNavigateTo = thisTypeName });
                    break;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string outputFile)
            {
                await AddMedia([outputFile], viewModel.Media.IndexOf(currentActionMedium) + 1);
            }

            if (e.Parameter is List<string> outputFiles)
            {
                await AddMedia(outputFiles.ToArray(), viewModel.Media.IndexOf(currentActionMedium) + 1);
            }

            currentActionMedium = null;
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ffmpegPath = Path.Join(Package.Current.InstalledLocation.Path, "Assets/ffmpeg.exe");
            }
            catch (InvalidOperationException)
            {
                ffmpegPath = "Assets/ffmpeg.exe";
            }
            if (!File.Exists(ffmpegPath))
            {
                await ErrorDialog.ShowAsync();
                return;
            }
            processor = new Processor(ffmpegPath);
        }

        private void SelectAll_OnClick(object sender, RoutedEventArgs e)
        {
            if(viewModel.AllAreSelected)
                foreach (var medium in viewModel.Media) medium.IsSelected = false;
            else 
                foreach (var medium in viewModel.Media) medium.IsSelected = true;
        }

        private void RemoveSingle_OnClick(object sender, RoutedEventArgs e)
        {
            var button = (AppBarButton)sender;
            var medium = (Medium)button.DataContext;
            viewModel.Media.Remove(medium);
        }

        private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            var commandBar = (CommandBar)sender;
            var medium = (Medium)commandBar.DataContext;
            commandBar.PrimaryCommands.Clear();
            commandBar.SecondaryCommands.Clear();
            foreach (var actionModel in medium.ActionModels)
            {
                var btn = new AppBarButton
                {
                    Icon = new FontIcon{ Glyph = actionModel.Icon },
                    Label = actionModel.Text,
                    LabelPosition = CommandBarLabelPosition.Collapsed
                };
                btn.DataContext = actionModel;
                btn.Click += ActionClicked;
                commandBar.PrimaryCommands.Add(btn);
            }

            var deleteButton = new AppBarButton { Icon = new SymbolIcon(Symbol.Delete), Label = "Delete" };
            deleteButton.Click += RemoveSingle_OnClick;
            commandBar.SecondaryCommands.Add(deleteButton);
        }

        private void MixSelected_OnClick(object sender, RoutedEventArgs e)
        {
            currentActionMedium = viewModel.Media.Last(m => m.IsSelected);
            var mediaPath = viewModel.Media.Select(m => m.FilePath);
            var thisTypeName = typeof(MainPage).FullName;
            Frame.Navigate(typeof(MediaTrackMixerMainPage), new MixerProps { FfmpegPath = ffmpegPath, MediaPaths = mediaPath, TypeToNavigateTo = thisTypeName });
        }

        private void MergeSelected_OnClick(object sender, RoutedEventArgs e)
        {
            currentActionMedium = viewModel.Media.Last(m => m.IsSelected);
            var mediaPath = viewModel.Media.Select(m => m.FilePath);
            var thisTypeName = typeof(MainPage).FullName;
            Frame.Navigate(typeof(ConcatMediaPage.ConcatMediaPage), new ConcatProps { FfmpegPath = ffmpegPath, MediaPaths = mediaPath, TypeToNavigateTo = thisTypeName });
        }
    }

    public class ImageUriConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            var path = (string)value;
            return string.IsNullOrWhiteSpace(path) ? null : new BitmapImage(new Uri("file:///" + path));
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
