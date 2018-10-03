using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace FaceFinder
{
    /// <summary>
    /// Processes image files to detect faces, attributes, and other info.
    /// Dependencies: ImageProcessor & FaceProcessor.
    /// </summary>
    class FaceFinderVM : ViewModelBase
    {
        // TODO: bind to ui & choose
        private const SearchOption searchOption = SearchOption.TopDirectoryOnly;

        private const string isolatedStorageFile = "FaceFinderStorage.txt";
        private const string thumbnailsFolderName = "FaceThumbnails";

        // Defaults associated with free tier, S0.
        private const string _computerVisionEndpoint =
            "https://westcentralus.api.cognitive.microsoft.com";
        private const string _faceEndpoint =
            "https://westcentralus.api.cognitive.microsoft.com";

        private readonly string male = Gender.Male.ToString();
        private readonly string female = Gender.Female.ToString();

        private CancellationTokenSource cancellationTokenSource;
        private FileInfo[] imageFiles = Array.Empty<FileInfo>();

    #region Bound properties
        private int splashZIndex = 1;
        public int SplashZIndex
        {
            get => splashZIndex;
            set => SetProperty(ref splashZIndex, value);
        }
        private bool splashVisibilty = true;
        public bool SplashVisibilty
        {
            get => splashVisibilty;
            set => SetProperty(ref splashVisibilty, value);
        }

        private string computerVisionKey = string.Empty;
        public string ComputerVisionKey
        {
            get => computerVisionKey;
            set
            {
                SetProperty(ref computerVisionKey, value);
                SaveDataToIsolatedStorage();
            }
        }
        private string computerVisionEndpoint = _computerVisionEndpoint;
        public string ComputerVisionEndpoint
        {
            get => computerVisionEndpoint;
            set
            {
                SetProperty(ref computerVisionEndpoint, value);
                SaveDataToIsolatedStorage();
            }
        }
        private string faceKey = string.Empty;
        public string FaceKey
        {
            get => faceKey;
            set
            {
                SetProperty(ref faceKey, value);
                SaveDataToIsolatedStorage();
            }
        }
        private string faceEndpoint = _faceEndpoint;
        public string FaceEndpoint
        {
            get => faceEndpoint;
            set
            {
                SetProperty(ref faceEndpoint, value);
                SaveDataToIsolatedStorage();
            }
        }

        private int fileCount;
        public int FileCount
        {
            get => fileCount;
            set => SetProperty(ref fileCount, value);
        }
        private int imageCount;
        public int ImageCount
        {
            get => imageCount;
            set => SetProperty(ref imageCount, value);
        }
        private int processingCount;
        public int ProcessingCount
        {
            get => processingCount;
            set => SetProperty(ref processingCount, value);
        }
        private int searchedCount;
        public int SearchedCount
        {
            get => searchedCount;
            set => SetProperty(ref searchedCount, value);
        }
        private int faceImageCount;
        public int FaceImageCount
        {
            get => faceImageCount;
            set => SetProperty(ref faceImageCount, value);
        }
        private int faceCount;
        public int FaceCount
        {
            get => faceCount;
            set => SetProperty(ref faceCount, value);
        }

        private string searchedForPerson;
        public string SearchedForPerson
        {
            get => searchedForPerson;
            set
            {
                SetProperty(ref searchedForPerson, value);
                AddPersonCommand.Execute(string.Empty);
            }
        }

        private string selectedFolder = string.Empty;
        public string SelectedFolder
        {
            get => selectedFolder;
            set
            {
                string selectedFolderName = (new DirectoryInfo(value)).Name;
                SetProperty(ref selectedFolder, selectedFolderName);
            }
        }

        // IsChecked
        private bool isSettingsExpanded;
        public bool IsSettingsExpanded
        {
            get => isSettingsExpanded;
            set => SetProperty(ref isSettingsExpanded, value);
        }

        private bool searchSubfolders;
        public bool SearchSubfolders
        {
            get => searchSubfolders;
            set => SetProperty(ref searchSubfolders, value);
        }

        private bool displayFileName = true;
        public bool DisplayFileName
        {
            get => displayFileName;
            set => SetProperty(ref displayFileName, value);
        }
        private bool displayAttributes = true;
        public bool DisplayAttributes
        {
            get => displayAttributes;
            set
            {
                SetProperty(ref displayAttributes, value);
            }
        }
        private bool getThumbnail = true;
        public bool GetThumbnail
        {
            get => getThumbnail;
            set => SetProperty(ref getThumbnail, value);
        }
        private bool getMetadata;
        public bool GetMetadata
        {
            get => getMetadata;
            set => SetProperty(ref getMetadata, value);
        }
        private bool getOCR;
        public bool GetOCR
        {
            get => getOCR;
            set => SetProperty(ref getOCR, value);
        }
        private bool getCaption;
        public bool GetCaption
        {
            get => getCaption;
            set => SetProperty(ref getCaption, value);
        }

        private bool showFaces;
        public bool ShowFaces
        {
            get => showFaces;
            set => SetProperty(ref showFaces, value);
        }

        private bool isMale;
        public bool IsMale
        {
            get => isMale;
            set => SetProperty(ref isMale, value);
        }
        private bool isFemale;
        public bool IsFemale
        {
            get => isFemale;
            set => SetProperty(ref isFemale, value);
        }
        private bool searchAge;
        public bool SearchAge
        {
            get => searchAge;
            set => SetProperty(ref searchAge, value);
        }

        private bool matchPerson;
        public bool MatchPerson
        {
            get => matchPerson;
            set => SetProperty(ref matchPerson, value);
        }

        private bool isPersonComboBoxOpen;
        public bool IsPersonComboBoxOpen
        {
            get => isPersonComboBoxOpen;
            set
            {
                // value == true onOpen, false onClose
                SetProperty(ref isPersonComboBoxOpen, value);

                // Populates personComboBox.
                if ((value && GroupNames.Count == 0) || !value)
                {
                    GetNamesCommand.Execute(string.Empty);
                }
            }
        }

        private double minAge = 10, maxAge = 80;
        public double MinAge
        {
            get => minAge;
            set => SetProperty(ref minAge, value);
        }
        public double MaxAge
        {
            get => maxAge;
            set => SetProperty(ref maxAge, value);
        }
    #endregion Bound properties

    #region Commands
        private ICommand selectFolderCommand;
        public ICommand SelectFolderCommand
        {
            get
            {
                return selectFolderCommand ??
                    (selectFolderCommand = new RelayCommand(
                        p => true, p => SelectFolder()));
            }
        }

        private ICommand getNamesCommand;
        public ICommand GetNamesCommand
        {
            get
            {
                return getNamesCommand ??
                    (getNamesCommand = new RelayCommand(
                        p => true, async p => await GetPersonNamesAsync()));
            }
        }

        private bool isAddPersonButtonEnabled = true;
        private ICommand addPersonCommand;
        public ICommand AddPersonCommand
        {
            get
            {
                return addPersonCommand ?? (addPersonCommand = new RelayCommand(
                    p => isAddPersonButtonEnabled,
                    async p => await AddPersonAsync(searchedForPerson)));
            }
        }
        private async Task AddPersonAsync(string person)
        {
            await faceProcessor.GetOrCreatePersonAsync(person, GroupInfos);

            // Disable person matching if PersonGroup not trained for this person
            if (!faceProcessor.IsPersonGroupTrained) { MatchPerson = false; }

            await GetPersonNamesAsync();
        }

        private bool isDeletePersonButtonEnabled = true;
        private ICommand deletePersonCommand;
        public ICommand DeletePersonCommand
        {
            get
            {
                return deletePersonCommand ?? (deletePersonCommand = new RelayCommand(
                    p => isDeletePersonButtonEnabled, 
                    async p => await DeletePersonAsync(searchedForPerson)));
            }
        }
        private async Task DeletePersonAsync(string person)
        {
            await faceProcessor.DeletePersonAsync(GroupInfos, GroupNames);
            if (GroupNames.Contains(searchedForPerson))
            {
                GroupNames.Remove(searchedForPerson);
            }
        }

        private bool isAddToPersonButtonEnabled = true;
        private ICommand addToPersonCommand;
        public ICommand AddToPersonCommand
        {
            get
            {
                return addToPersonCommand ?? (addToPersonCommand = new RelayCommand(
                    p => isAddToPersonButtonEnabled,
                    async p => await AddToPersonAsync(p)));
            }
        }

        private bool isFindFacesButtonEnabled;
        private ICommand findFacesCommand;
        public ICommand FindFacesCommand
        {
            get
            {
                return findFacesCommand ?? (findFacesCommand = new RelayCommand(
                    p => isFindFacesButtonEnabled, async p => await FindFacesAsync()));
            }
        }

        private bool isCancelButtonEnabled;
        private ICommand cancelFindFacesCommand;
        public ICommand CancelFindFacesCommand
        {
            get
            {
                return cancelFindFacesCommand ?? (cancelFindFacesCommand = new RelayCommand(
                        p => isCancelButtonEnabled, p => CancelFindFaces()));
            }
        }
    #endregion Commands

        public ImageProcessor imageProcessor { get; set; }
        public FaceProcessor faceProcessor { get; set; }
        public ObservableCollection<ImageInfo> ImageInfos { get; set; }
        public ObservableCollection<ImageInfo> GroupInfos { get; set; }
        public ObservableCollection<string> GroupNames { get; set; }

        public FaceFinderVM()
        {
            GetDataFromIsolatedStorage();
            ImageInfos = new ObservableCollection<ImageInfo>();
            GroupInfos = new ObservableCollection<ImageInfo>();
            GroupNames = new ObservableCollection<string>();
            SetupVisionServices();
        }

        private void SetupVisionServices()
        {
            App app = (App)Application.Current;

            app.SetupComputerVisionClient(ComputerVisionKey, ComputerVisionEndpoint);
            imageProcessor = new ImageProcessor(app.computerVisionClient);

            app.SetupFaceClient(FaceKey, FaceEndpoint);
            faceProcessor = new FaceProcessor(app.faceClient);
        }

        private async Task FindFacesAsync()
        {
            if (ComputerVisionKey.Equals(string.Empty) || FaceKey.Equals(string.Empty))
            {
                IsSettingsExpanded = true;
                MessageBox.Show("Enter your subscription key(s) in the dialog",
                    "Missing subscription key(s)", 
                    MessageBoxButton.OKCancel, MessageBoxImage.Asterisk);
                return;
            }

            isFindFacesButtonEnabled = false;

            ImageInfos.Clear();

            isCancelButtonEnabled = true;
            cancellationTokenSource = new CancellationTokenSource();

            await ProcessImageFilesForFacesAsync(imageFiles, cancellationTokenSource.Token);

            cancellationTokenSource.Dispose();
            isCancelButtonEnabled = false;

            isFindFacesButtonEnabled = true;

            // TODO: without this statement, app suspends updating UI until explicit focus change (mouse or key event)
            await Task.Delay(1);
        }
        private void CancelFindFaces()
        {
            isCancelButtonEnabled = false;
            cancellationTokenSource.Cancel();
        }

        // The root of image processing. Calls all the other image processing methods when a face is detected.
        private async Task ProcessImageFilesForFacesAsync(
            FileInfo[] imageFiles, CancellationToken cancellationToken)
        {
            string thumbnailsFolder = imageFiles[0].DirectoryName +
                Path.DirectorySeparatorChar + thumbnailsFolderName;
            if (!Directory.Exists(thumbnailsFolder))
            {
                Directory.CreateDirectory(thumbnailsFolder);
            }

            ProcessingCount = 0;    // # of image files processed
            SearchedCount = 0;      // images containing a face
            FaceImageCount = 0;     // images with a face matching the search criteria
            FaceCount = 0;          // images with a face matching the search criteria and selected person

            IList<DetectedFace> faceList;
            foreach (FileInfo file in imageFiles)
            {
                if (cancellationToken.IsCancellationRequested) { return; }

                ProcessingCount++;
                try
                {
                    using (FileStream stream = file.OpenRead())
                    {
                        faceList = await faceProcessor.GetFaceListAsync(stream);
                    }

                    // Ignore image files without a detected face
                    if (faceList.Count > 0)
                    {
                        SearchedCount++;
                        int matchedCount = 0;

                        // Holds info about the currently analyzed image file and detected
                        ImageInfo newImage = new ImageInfo();
                        newImage.FilePath = file.DirectoryName + Path.DirectorySeparatorChar + file.Name;
                        newImage.FileName = file.Name;

                        if (getMetadata)
                        {
                            GetImageMetadata(file, newImage);
                        }

                        string attributes = string.Empty;
                        bool isImageMatch = false;

                        foreach (DetectedFace face in faceList)
                        {
                            double? age = face.FaceAttributes.Age;
                            string gender = face.FaceAttributes.Gender.ToString();

                            if (searchAge && ((age < MinAge) || (age > MaxAge))) { continue; }
                            if (isMale && !gender.Equals(male)) { continue; }
                            if (isFemale && !gender.Equals(female)) { continue; }
                            attributes += gender + " " + age + "   ";

                            matchedCount++;

                            // If match on face, call faceProcessor
                            if (MatchPerson && faceProcessor.IsPersonGroupTrained)
                            {
                                bool isFaceMatch = await faceProcessor.MatchFaceAsync(
                                    (Guid)face.FaceId, newImage);
                                isImageMatch |= isFaceMatch;
                            }
                        }

                        // No faces matched search criteria
                        if (matchedCount == 0) { continue; }
                        if (MatchPerson && !isImageMatch) { continue; }

                        newImage.Attributes = attributes;
                        FaceImageCount += matchedCount;

                        var tasks = new List<Task>();

                        if (getThumbnail)
                        {
                            Task thumbTask = imageProcessor.ProcessImageFileForThumbAsync(file, newImage, thumbnailsFolder);
                            tasks.Add(thumbTask);
                        }
                        else
                        {
                            // Use local image
                            newImage.ThumbUrl = file.FullName;
                        }
                        if (getCaption)
                        {
                            Task captionTask = imageProcessor.ProcessImageFileForCaptionAsync(file, newImage);
                            tasks.Add(captionTask);
                        }
                        if (getOCR)
                        {
                            Task ocrTask = imageProcessor.ProcessImageFileForTextAsync(file, newImage);
                            tasks.Add(ocrTask);
                        }

                        if (tasks.Count != 0)
                        {
                            await Task.WhenAll(tasks);
                        }

                        ImageInfos.Add(newImage);

                        if (MatchPerson)
                        {
                            FaceCount = ImageInfos.Count;
                        }
                    }
                }
                // Catch and display Face errors.
                catch (APIErrorException fe)
                {
                    Debug.WriteLine("ProcessImageFilesForFacesAsync, api: " + fe.Message);
                    MessageBox.Show(fe.Message + ": " + file.Name, "ProcessImageFilesForFacesAsync");
                    break;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("ProcessImageFilesForFacesAsync: " + e.Message);
                    MessageBox.Show(e.Message + ": " + file.Name, "ProcessImageFilesForFacesAsync");
                    break;
                }
            }
        }

        private string GetImageMetadata(FileInfo file, ImageInfo newImage)
        {
            if (!getMetadata) { return string.Empty; }

            var dateTaken = string.Empty;
            var title = string.Empty;

            try
            {
                using (FileStream fileStream = file.OpenRead())
                {
                    BitmapFrame bitmapFrame = BitmapFrame.Create(
                        fileStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                    BitmapMetadata bitmapMetadata = bitmapFrame.Metadata as BitmapMetadata;
                    if (bitmapMetadata?.DateTaken != null)
                    {
                        dateTaken = bitmapMetadata?.DateTaken;
                    }
                    // Throws NotSupportedException on png's (bitmap codec does not support the bitmap property)
                    if (bitmapMetadata?.Title != null)
                    {
                        title = bitmapMetadata?.Title;
                    }
                }
            }
            catch (NotSupportedException e)
            {
                Debug.WriteLine("GetImageMetadata: " + file.Name + "\t" + e.Message);
            }

            var metadata = dateTaken + " " + title;
            if (metadata.Equals(" ")) { metadata = string.Empty; }

            newImage.Metadata = metadata;
            return metadata;
        }

        // Called by IsPersonComboBoxOpen setter
        private async Task GetPersonNamesAsync()
        {
            IList<string> personNames = await faceProcessor.GetAllPersonNamesAsync();
            foreach (string name in personNames)
            {
                if (!GroupNames.Contains(name))
                {
                    GroupNames.Add(name);
                }
            }
        }

        private async Task AddToPersonAsync(object selectedThumbnails)
        {
            if (string.IsNullOrWhiteSpace(SearchedForPerson)) { return; }

            IList selectedItems = (IList)selectedThumbnails;
            if(selectedItems.Count == 0) { return; }

            IList<ImageInfo> items = selectedItems.Cast<ImageInfo>().ToList();
            await faceProcessor.AddFacesToPersonAsync(items, GroupInfos);
            ShowFaces = true;
        }

        private void GetDataFromIsolatedStorage()
        {
            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(
                IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                if (!isoStore.FileExists(isolatedStorageFile))
                {
                    return;
                }

                using (var reader = new StreamReader(isoStore.OpenFile(isolatedStorageFile,
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    ComputerVisionKey = reader.ReadLine();
                    ComputerVisionEndpoint = reader.ReadLine();
                    FaceKey = reader.ReadLine();
                    FaceEndpoint = reader.ReadLine();
                }

                if (ComputerVisionEndpoint.Equals(string.Empty))
                {
                    ComputerVisionEndpoint = _computerVisionEndpoint;
                }
                if (FaceEndpoint.Equals(string.Empty))
                {
                    FaceEndpoint = _faceEndpoint;
                }
            }
        }

        private void SaveDataToIsolatedStorage()
        {
            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(
                IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                using (var writer = new StreamWriter(isoStore.OpenFile(isolatedStorageFile,
                    FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine(ComputerVisionKey);
                    writer.WriteLine(ComputerVisionEndpoint);
                    writer.WriteLine(FaceKey);
                    writer.WriteLine(FaceEndpoint);
                }
            }

            SetupVisionServices();
        }

        private void SelectFolder()
        {
            string folderPath = PickFolder();
            if (folderPath == string.Empty) { return; }

            SelectedFolder = folderPath;

            imageFiles = GetImageFiles(folderPath);
            if (imageFiles.Length == 0)
            {
                isFindFacesButtonEnabled = false;
                return;
            }

            // Hide splash image.
            SplashZIndex = 0;
            SplashVisibilty = false;

            isFindFacesButtonEnabled = true;
        }

        // Windows.Forms.FolderBrowserDialog doesn't allow setting the
        // initial view to a specific folder, only an Environment.SpecialFolder.
        private string PickFolder()
        {
            using (var folderPicker = new System.Windows.Forms.FolderBrowserDialog())
            {
                string folderPath = string.Empty;

                folderPicker.Description = "Face Finder";
                folderPicker.RootFolder = Environment.SpecialFolder.MyComputer;
                folderPicker.ShowNewFolderButton = false;

                var dialogResult = folderPicker.ShowDialog();
                if (dialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    folderPath = folderPicker.SelectedPath;
                }
                return folderPath;
            }
        }

        private FileInfo[] GetImageFiles(string folder)
        {
            DirectoryInfo di = new DirectoryInfo(folder);
            FileCount = di.GetFiles("*.*", searchOption).Length;

            FileInfo[] bmpFiles = di.GetFiles("*.bmp", searchOption);
            FileInfo[] gifFiles = di.GetFiles("*.gif", searchOption);
            FileInfo[] jpgFiles = di.GetFiles("*.jpg", searchOption);
            FileInfo[] pngFiles = di.GetFiles("*.png", searchOption);
            FileInfo[] allImageFiles =
                bmpFiles.Concat(gifFiles).Concat(jpgFiles).Concat(pngFiles).ToArray();

            ImageCount = allImageFiles.Length;

            return allImageFiles;
        }
    }
}
