using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;  
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

namespace CognitiveServicesPlayground
{
    public sealed partial class FacePage : BindablePage
    {
        private StorageFile currentImageFile;
        private FaceServiceClient client;
        private DispatcherTimer trainingStatusTimer;

        public FacePage()
        {
            this.InitializeComponent();

            trainingStatusTimer = new DispatcherTimer();
            trainingStatusTimer.Tick += OnCheckTrainingStatus;
            trainingStatusTimer.Interval = TimeSpan.FromSeconds(1);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            MicrosoftCognitiveServicesFaceKey = await LoadKeyAsync();
            await InitializeServiceAsync();
        }

        private async Task InitializeServiceAsync()
        {
            if (!String.IsNullOrEmpty(MicrosoftCognitiveServicesFaceKey))
            {
                client = new FaceServiceClient(MicrosoftCognitiveServicesFaceKey, "https://westeurope.api.cognitive.microsoft.com/face/v1.0");
                await GetPersonGroupsAsync();
            }
        }

        private PersonGroup[] personGroups;
        public PersonGroup[] PersonGroups
        {
            get => personGroups;
            set => SetProperty(ref personGroups, value);
        }

        private string microsoftCognitiveServicesFaceKey;
        public string MicrosoftCognitiveServicesFaceKey
        {
            get => microsoftCognitiveServicesFaceKey;
            set => SetProperty(ref microsoftCognitiveServicesFaceKey, value);
        }

        private Face[] detectedFaces;
        public Face[] DetectedFaces
        {
            get => detectedFaces;
            set => SetProperty(ref detectedFaces, value);
        }

        private PersonGroup selectedPersonGroup;
        public PersonGroup SelectedPersonGroup
        {
            get => selectedPersonGroup;
            set => SetProperty(ref selectedPersonGroup, value);
        }

        private TrainingStatus selectedPersonGroupTrainingStatus;
        public TrainingStatus SelectedPersonGroupTrainingStatus
        {
            get => selectedPersonGroupTrainingStatus;
            set => SetProperty(ref selectedPersonGroupTrainingStatus, value);
        }

        private Person[] selectedPersonGroupPersons;
        public Person[] SelectedPersonGroupPersons
        {
            get => selectedPersonGroupPersons;
            set => SetProperty(ref selectedPersonGroupPersons, value);
        }

        private Person selectedPerson;
        public Person SelectedPerson
        {
            get => selectedPerson;
            set
            {
                if (SetProperty(ref selectedPerson, value))
                {
                    SelectedPersonFaceCount = value.PersistedFaceIds.Length.ToString();
                }
            }
        }

        private string selectedPersonFaceCount;
        public string SelectedPersonFaceCount
        {
            get => selectedPersonFaceCount;
            set => SetProperty(ref selectedPersonFaceCount, value);
        }

        private async void OnOpenImage(object sender, RoutedEventArgs e)
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

                await DetectFaces(stream);
            }                                                                                

            FacesGrid.Children.Clear();
            var red = new SolidColorBrush(Colors.Red);
            var white = new SolidColorBrush(Colors.White);
            var transparent = new SolidColorBrush(Colors.Red){Opacity=.1};

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

                grid.Tapped += OnFaceTapped;

                var textBlock = new TextBlock { Foreground = white };

                var border = new Border
                {
                    Padding = new Thickness(4),
                    Background = red,
                    BorderThickness = new Thickness(0),
                    Visibility = Visibility.Collapsed,
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

        private void OnFaceTapped(object sender, TappedRoutedEventArgs e)
        {
            var face = (Face)((Grid)sender).DataContext;
            var person = SelectedPerson;

            var menu = new MenuFlyout();
            var item1 = new MenuFlyoutItem {Text = "Add this face to selected person"};
            item1.Click += async (ss, ee) =>
            {
                if (person == null)
                {
                    await new MessageDialog("Please select a person.", "Add face to person").ShowAsync();
                    return;
                }
                menu.ShowAt((FrameworkElement)sender);
                using (var stream = await currentImageFile.OpenStreamForReadAsync())
                {
                    await AddPersonFace(stream, face.FaceRectangle);
                }
            };
            menu.Items.Add(item1);

            var item2 = new MenuFlyoutItem { Text = "Identify this face" };
            item2.Click += (ss, ee) =>
            {
                IdentifyPhoto();
            };
            menu.Items.Add(item2);
            menu.ShowAt(sender as FrameworkElement);
        }
        private async void IdentifyPhoto()
        {
            var persons = await IdentifyFaces(DetectedFaces.Select(i => i.FaceId).ToArray());
            foreach (var sp in FacesGrid.Children)
            {
                var tb = sp.FindVisualChild<TextBlock>();
                var border = sp.FindVisualChild<Border>();
                var pe = tb.DataContext as Face;

                var faceId = pe.FaceId;

                var person = persons.SingleOrDefault(i => i.Item1 == faceId);
                if (person == null)
                {
                    tb.Text = "Unknown";
                }
                else
                {
                    tb.Text = person.Item2.Name;
                }
                border.Visibility = Visibility.Visible;
            }
        }

        public async void OnAddPersonGroup()
        {
            var name = PersonGroupNameBox.Text;
            if (String.IsNullOrEmpty(name))
            {
                await new MessageDialog("Please enter group's name.", "Add person group").ShowAsync();
                return;
            }

            try
            {
                IsBusy = true;
                await client.CreatePersonGroupAsync(Guid.NewGuid().ToString(), name);
                await GetPersonGroupsAsync();
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Create person group").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnPersonGroupChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                await GetPersonGroupAsync(((PersonGroup)e.AddedItems[0]).PersonGroupId);
            }
        }

        private async void OnPersonChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                await GetPersonAsync(((Person)e.AddedItems[0]).PersonId);
            }
        }

        public async void OnAddPerson()
        {
            if (SelectedPersonGroup == null)
            {
                await new MessageDialog("Please select a person group.", "Add person").ShowAsync();
                return;
            }

            var name = PersonNameBox.Text;
            if (String.IsNullOrEmpty(name))
            {
                await new MessageDialog("Please enter person's name.", "Add person").ShowAsync();
                return;
            }

            try
            {
                IsBusy = true;
                var personGroupId = SelectedPersonGroup.PersonGroupId;
                var result = await client.CreatePersonAsync(personGroupId, name);
                SelectedPersonGroupPersons = await client.ListPersonsAsync(personGroupId);
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Add person").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }


        public async Task GetPersonGroupsAsync()
        {
            try
            {
                IsBusy = true;
                PersonGroups = await client.ListPersonGroupsAsync();
            }
            catch (FaceAPIException ex)
            {
                PersonGroups = null;
                await new MessageDialog(ex.ErrorMessage, "Get person group").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task TrainPersonGroupAsync()
        {
            if (SelectedPersonGroup == null)
            {
                await new MessageDialog("Please select a person group.", "Train person group").ShowAsync();
                return;
            }

            try
            {
                IsBusy = true;
                await client.TrainPersonGroupAsync(SelectedPersonGroup.PersonGroupId);
                SelectedPersonGroupTrainingStatus = await client.GetPersonGroupTrainingStatusAsync(SelectedPersonGroup.PersonGroupId);
                trainingStatusTimer.Start();
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Train person group").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnCheckTrainingStatus(object sender, object e)
        {
            SelectedPersonGroupTrainingStatus = await client.GetPersonGroupTrainingStatusAsync(SelectedPersonGroup.PersonGroupId);
            if (SelectedPersonGroupTrainingStatus == null || SelectedPersonGroupTrainingStatus.Status != Status.Running)
            {
                trainingStatusTimer.Stop();
            }
        }

        public async Task GetPersonGroupAsync(string personGroupId)
        {
            try
            {
                IsBusy = true;
                SelectedPersonGroup = await client.GetPersonGroupAsync(personGroupId);
                SelectedPersonGroupPersons = await client.ListPersonsAsync(personGroupId);
                SelectedPersonGroupTrainingStatus = await client.GetPersonGroupTrainingStatusAsync(personGroupId);
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Get person group").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task GetPersonAsync(Guid personId)
        {
            try
            {
                IsBusy = true;
                SelectedPerson = await client.GetPersonAsync(SelectedPersonGroup.PersonGroupId, personId);
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Get person").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task DetectFaces(Stream stream)
        {
            try
            {
                IsBusy = true;
                DetectedFaces = await client.DetectAsync(stream, true, false, null);
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Detect faces").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task AddPersonFace(Stream stream, FaceRectangle rectangle)
        {
            try
            {
                IsBusy = true;
                var result = await client.AddPersonFaceAsync(SelectedPersonGroup.PersonGroupId, SelectedPerson.PersonId, stream, null, rectangle);
                await GetPersonAsync(SelectedPerson.PersonId);
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Add person face").ShowAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<Tuple<Guid, Person>[]> IdentifyFaces(Guid[] faceIds)
        {
            if (SelectedPersonGroup == null)
            {
                await new MessageDialog("Please select a person group.", "Train person group").ShowAsync();
                return new Tuple<Guid, Person>[0];
            }

            try
            {
                IsBusy = true;
                var list = new List<Tuple<Guid, Person>>();
                var result = await client.IdentifyAsync(SelectedPersonGroup.PersonGroupId, faceIds);
                foreach (var identifyResult in result)
                {
                    var c = identifyResult.Candidates.FirstOrDefault();
                    if (c != null)
                    {
                        var pe = selectedPersonGroupPersons.SingleOrDefault(i => i.PersonId == c.PersonId);
                        list.Add(new Tuple<Guid, Person>(identifyResult.FaceId, pe));
                    }
                }
                return list.ToArray();
            }
            catch (FaceAPIException ex)
            {
                await new MessageDialog(ex.ErrorMessage, "Identify faces").ShowAsync();
                return new Tuple<Guid, Person>[0];
            }
            finally
            {
                IsBusy = false;
            }
        }


        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set => SetProperty(ref isBusy, value);
        }

        private async void OnApplyFaceApiKey(object sender, RoutedEventArgs e)
        {
            await InitializeServiceAsync();
            if (PersonGroups == null)
            {
                return;
            }

            var dialog = new MessageDialog("Excellent, do you want to save this key for future use?", "Face API Key applied");
            dialog.Commands.Add(new UICommand("Yes") { Id = 0 });
            dialog.Commands.Add(new UICommand("No") { Id = 1 });
            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                await SaveKeyAsync(MicrosoftCognitiveServicesFaceKey);
            }

            if (PersonGroups.Length > 0)
            {
                await new MessageDialog("Great, now check your existing groups on the left.", "Face API Key applied").ShowAsync();
            }
            else
            {
                await new MessageDialog("Great, you can now start adding person groups.", "Face API Key applied").ShowAsync();
            }
        }

        private static async Task SaveKeyAsync(string key)
        {
            var file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("faceapikey.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, key);
        }

        private static async Task<string> LoadKeyAsync()
        {
            var file = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync("faceapikey.txt");
            if (file != null)
            {
                return await FileIO.ReadTextAsync((StorageFile)file);
            }
            return null;
        }
    }
}