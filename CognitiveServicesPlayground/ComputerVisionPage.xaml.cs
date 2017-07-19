using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;

namespace CognitiveServicesPlayground
{
    public sealed partial class ComputerVisionPage : BindablePage
    {
        private StorageFile currentImageFile;
        private VisionServiceClient visionClient;
        private Model[] models;

        public ComputerVisionPage()
        {
            this.InitializeComponent();
        }


        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set => SetProperty(ref isBusy, value);
        }

        public Model[] Models
        {
            get => models;
            set => SetProperty(ref models, value);
        }


        private Tag[] _tags;
        public Tag[] Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        private Face[] detectedFaces;
        public Face[] DetectedFaces
        {
            get => detectedFaces;
            set => SetProperty(ref detectedFaces, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private Region region;
        public Region Region
        {
            get => region;
            set => SetProperty(ref region, value);
        }
        private Word[] words;
        public Word[] Words
        {
            get => words;
            set => SetProperty(ref words, value);
        }

        public async Task<bool> InitializeServiceAsync(VisionServiceClient visionClient)
        {
            this.visionClient = visionClient;
            await ListDomainSpecificModelsAsync();
            return Models != null;
        }

        public async Task ListDomainSpecificModelsAsync()
        {
            try
            {
                IsBusy = true;
                var result = await visionClient.ListModelsAsync();
                Models = result?.Models;
            }
            catch (ClientException ex)
            {
                Models = null;
                await new MessageDialog(ex.Error.Message, "Get models").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }


        public async void OnOpenImage(object sender, RoutedEventArgs routedEventArgs)
        {
            var fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.FileTypeFilter.Add(".jpg");
            fileOpenPicker.FileTypeFilter.Add(".jpeg");
            fileOpenPicker.FileTypeFilter.Add(".gif");
            fileOpenPicker.FileTypeFilter.Add(".bmp");
            fileOpenPicker.FileTypeFilter.Add(".png");
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;

            currentImageFile = await fileOpenPicker.PickSingleFileAsync();
            if (currentImageFile == null) return;

            using (var stream = await currentImageFile.OpenStreamForReadAsync())
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                Image.Source = bitmap;
                bitmap.Stop();
                stream.Seek(0, SeekOrigin.Begin);

                await AnalyzeImageAsync(stream);

                stream.Seek(0, SeekOrigin.Begin);
                await RecognizeTextAsync(stream);
            }

            FacesGrid.Children.Clear();
            var red = new SolidColorBrush(Colors.Red);
            var white = new SolidColorBrush(Colors.White);
            var transparent = new SolidColorBrush(Colors.Red) {Opacity = .1};

            if (DetectedFaces != null)
            {
                foreach (var face in DetectedFaces)
                {
                    var grid = new Grid
                    {
                        Width = face.FaceRectangle.Width,
                        Height = face.FaceRectangle.Height,
                        BorderThickness = new Thickness(4),
                        BorderBrush = red,
                        Background = transparent
                    };

//                grid.Tapped += OnFaceTapped;

                    var textBlock = new TextBlock {Foreground = white};
                    textBlock.Text = $"{face.Gender} ({face.Age})";

                    var border = new Border
                    {
                        Padding = new Thickness(4),
                        Background = red,
                        BorderThickness = new Thickness(0),
                        Visibility = Visibility.Visible,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = textBlock,
                    };

                    var stackPanel = new StackPanel();
                    stackPanel.Margin = new Thickness(face.FaceRectangle.Left, face.FaceRectangle.Top, 0, 0);
                    stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                    stackPanel.VerticalAlignment = VerticalAlignment.Top;
                    stackPanel.Children.Add(grid);
                    stackPanel.Children.Add(border);
                    stackPanel.DataContext = face;

                    FacesGrid.Children.Add(stackPanel);
                }
            }

            if (Region != null)
            {
                foreach (var line in Region.Lines)
                {
                    var textBlock = new TextBlock();
                    textBlock.Foreground = white;
                    textBlock.Text = String.Join(" ", line.Words.Select(i => i.Text));

                    var border = new Border
                    {
                        Padding = new Thickness(4),
                        Background = red,
                        BorderThickness = new Thickness(0),
                        Visibility = Visibility.Visible,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = textBlock,
                    };


                    var sp = new StackPanel();
                    sp.Margin = new Thickness(line.Rectangle.Left, line.Rectangle.Top, 0, 0);
                    sp.HorizontalAlignment = HorizontalAlignment.Left;
                    sp.VerticalAlignment = VerticalAlignment.Top;
                    sp.Children.Add(border);
                    sp.DataContext = line;

                    FacesGrid.Children.Add(sp);
                }
            }
        }

        public async Task AnalyzeImageAsync(Stream stream)
        {
            try
            {
                IsBusy = true;
                var result = await visionClient.AnalyzeImageAsync(stream, new[] {VisualFeature.Description, VisualFeature.Categories, VisualFeature.Faces, VisualFeature.Tags});
                DetectedFaces = result?.Faces;
                Tags = result?.Tags;
                Description = result?.Description?.Captions?.FirstOrDefault().Text;
                var categories = result?.Categories;
            }
            catch (ClientException ex)
            {
                await new MessageDialog(ex.Error.Message, "Analyze image").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RecognizeTextAsync(Stream stream)
        {
            try
            {
                IsBusy = true;

                stream.Seek(0, SeekOrigin.Begin);
                var result = await visionClient.RecognizeTextAsync(stream);
                Region = result?.Regions?.FirstOrDefault();
                Words = Region?.Lines?.FirstOrDefault()?.Words;
            }
            catch (ClientException ex)
            {
                await new MessageDialog(ex.Error.Message, "Recognize text").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }


    }
}