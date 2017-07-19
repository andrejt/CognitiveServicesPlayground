using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Vision;

namespace CognitiveServicesPlayground
{
    public sealed partial class MainPage : BindablePage
    {
        private FaceServiceClient faceClient;
        private VisionServiceClient visionClient;

        public MainPage()
        {
            InitializeComponent();
            SelectedNavigationItem = NavigationItems[0];
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadKeysAsync();
        }

        private async Task LoadKeysAsync()
        {
            var file = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync("faceapikey.txt");
            if (file != null)
            {
                MicrosoftCognitiveServicesFaceKey = await FileIO.ReadTextAsync((StorageFile) file);
                faceClient = new FaceServiceClient(MicrosoftCognitiveServicesFaceKey, "https://westeurope.api.cognitive.microsoft.com/face/v1.0");
            }
            else
            {
                MicrosoftCognitiveServicesFaceKey = null;
            }

            file = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync("visionapikey.txt");
            if (file != null)
            {
                MicrosoftCognitiveServicesVisionKey = await FileIO.ReadTextAsync((StorageFile)file);
                visionClient = new VisionServiceClient(MicrosoftCognitiveServicesVisionKey, "https://westeurope.api.cognitive.microsoft.com/vision/v1.0");
            }
            else
            {
                MicrosoftCognitiveServicesFaceKey = null;
            }
        }

        private async void OnApplyFaceApiKey(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(MicrosoftCognitiveServicesFaceKey))
            {
                return;
            }
            var page = RootFrame.Content as FacePage;
            if (page == null)
            {
                return;
            }
            faceClient = new FaceServiceClient(MicrosoftCognitiveServicesFaceKey, "https://westeurope.api.cognitive.microsoft.com/face/v1.0");

            var isInitialized = await page.InitializeServiceAsync(faceClient);
            if (!isInitialized)
            {
                return;
            }

            var dialog = new MessageDialog("Excellent, do you want to save this key for future use?", "Face API Key applied");
            dialog.Commands.Add(new UICommand("Yes") { Id = 0 });
            dialog.Commands.Add(new UICommand("No") { Id = 1 });
            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                await SaveFaceKeyAsync(MicrosoftCognitiveServicesFaceKey);
            }

            if (page.PersonGroups.Length > 0)
            {
                await new MessageDialog("Great, now check your existing groups on the left.", "Face API Key applied").ShowAsync();
            }
            else
            {
                await new MessageDialog("Great, you can now start adding person groups.", "Face API Key applied").ShowAsync();
            }
        }

        private async void OnApplyComputerVisionApiKey(object sender, RoutedEventArgs e)
        {
            await InitializeVisionPage();
        }

        private async Task InitializeVisionPage()
        {
            if (!String.IsNullOrEmpty(MicrosoftCognitiveServicesVisionKey))
            {
                return;
            }

            var page = RootFrame.Content as ComputerVisionPage;
            if (page == null)
            {
                return;
            }

            visionClient = new VisionServiceClient(MicrosoftCognitiveServicesVisionKey, "https://westeurope.api.cognitive.microsoft.com/vision/v1.0");
            var isInitialized = await page.InitializeServiceAsync(visionClient);
            if (!isInitialized)
            {
                return;
            }

            var dialog = new MessageDialog("Excellent, do you want to save this key for future use?", "Vision API Key applied");
            dialog.Commands.Add(new UICommand("Yes") {Id = 0});
            dialog.Commands.Add(new UICommand("No") {Id = 1});
            var result = await dialog.ShowAsync();
            if ((int) result.Id == 0)
            {
                await SaveVisionKeyAsync(MicrosoftCognitiveServicesVisionKey);
            }
            await new MessageDialog("Great, you can now start experimenting in image detection.", "Vision API Key applied")
                .ShowAsync();
        }

        private static async Task SaveFaceKeyAsync(string key)
        {
            var file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("faceapikey.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, key);
        }
        private static async Task SaveVisionKeyAsync(string key)
        {
            var file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("visionapikey.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, key);
        }

        private string microsoftCognitiveServicesFaceKey;
        public string MicrosoftCognitiveServicesFaceKey
        {
            get => microsoftCognitiveServicesFaceKey;
            set => SetProperty(ref microsoftCognitiveServicesFaceKey, value);
        }

        private string microsoftCognitiveServicesVisionKey;
        public string MicrosoftCognitiveServicesVisionKey
        {
            get => microsoftCognitiveServicesVisionKey;
            set => SetProperty(ref microsoftCognitiveServicesVisionKey, value);
        }

        public NavigationItem[] NavigationItems { get; } = 
        {
            new NavigationItem{Title = "Face", PageType = typeof(FacePage)},
            new NavigationItem{Title = "Vision", PageType = typeof(ComputerVisionPage)}
        };

        private NavigationItem _selectedNavigationItem;
        public NavigationItem SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set => SetProperty(ref _selectedNavigationItem, value);
        }

        private async void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(FacePage) && faceClient != null)
            {
                await ((FacePage)RootFrame.Content).InitializeServiceAsync(faceClient);
            }
            else if (e.SourcePageType == typeof(ComputerVisionPage) && visionClient != null)
            {
                await ((ComputerVisionPage)RootFrame.Content).InitializeServiceAsync(visionClient);
            }
        }
    }
}