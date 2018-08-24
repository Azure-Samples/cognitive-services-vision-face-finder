using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace FaceFinder
{
    /// <summary>
    /// Processes image files to detect faces, attributes, and other info.
    /// Dependencies: Computer Vision & Face services.
    /// </summary>
    class ImageProcessor : ViewModelBase
    {
        // LOW: bind to ui & choose
        private const SearchOption searchOption = SearchOption.TopDirectoryOnly;

        private const string isolatedStorageFile = "FaceFinderStorage.txt";
        private const string thumbnailsFolderName = "FaceThumbnails";

        // Defaults associated with free tier, F0.
        private const string _computerVisionEndpoint =
            "https://westcentralus.api.cognitive.microsoft.com";
        private const string _faceEndpoint =
            "https://westcentralus.api.cognitive.microsoft.com";

        private const int thumbWidth = 100, thumbHeight = 100;

        private readonly string male =
            Microsoft.Azure.CognitiveServices.Vision.Face.Models.Gender.Male.ToString();
        private readonly string female =
            Microsoft.Azure.CognitiveServices.Vision.Face.Models.Gender.Female.ToString();

        private IComputerVisionClient computerVisionClient;
        private IFaceClient faceClient;

        private CancellationTokenSource cancellationTokenSource;
        private FileInfo[] imageFiles = Array.Empty<FileInfo>();

    #region Bound properties
        private int splashZIndex = 1;
        public int SplashZIndex
        {
            get => splashZIndex;
            set => SetProperty(ref splashZIndex, value);
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

        private string searchedForPerson = "Faces to Match";
        public string SearchedForPerson
        {
            get => searchedForPerson;
            set
            {
                SetProperty(ref searchedForPerson, value);
                //GetGroupNamesAsync();
                CreateGroupCommand.Execute(searchedForPerson);
            }
            //set => SetProperty(ref searchedForPerson, value);
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
            set => SetProperty(ref displayAttributes, value);
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

        private bool matchFace;
        public bool MatchFace
        {
            get => matchFace;
            set
            {
                SetProperty(ref matchFace, value);
                GetGroupNamesAsync();
                //CreateGroupCommand.Execute(searchedForPerson);
            }
            //set => SetProperty(ref searchFaces, value);
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
                    (selectFolderCommand = new RelayCommand(p => true, p => SelectFolder()));
            }
        }

        private bool isCreateGroupButtonEnabled = true;
        private ICommand createGroupCommand;
        public ICommand CreateGroupCommand
        {
            get
            {
                return createGroupCommand ?? (createGroupCommand = new RelayCommand(
                    p => isCreateGroupButtonEnabled,
                    async p => await faceProcessor.CreatePersonGroupAsync(searchedForPerson, GroupInfos)));
            }
        }

        private bool isDeleteGroupButtonEnabled = true;
        private ICommand deleteGroupCommand;
        public ICommand DeleteGroupCommand
        {
            get
            {
                return deleteGroupCommand ?? (deleteGroupCommand = new RelayCommand(
                    p => isDeleteGroupButtonEnabled, 
                    async p => await DeleteAsync()));
            }
        }
        private async Task DeleteAsync()
        {
            await faceProcessor.DeletePersonGroupAsync(searchedForPerson);
            if (GroupNames.Contains(searchedForPerson))
            {
                GroupNames.Remove(searchedForPerson);
            }
        }

        private bool isAddToGroupButtonEnabled = true;
        private ICommand addToGroupCommand;
        public ICommand AddToGroupCommand
        {
            get
            {
                return addToGroupCommand ?? (addToGroupCommand = new RelayCommand(
                    p => isAddToGroupButtonEnabled,
                    async p => await AddToGroupAsync(p)));
            }
        }

        private bool isFindFacesButtonEnabled;
        private ICommand findFacesCommand;
        public ICommand FindFacesCommand
        {
            get
            {
                return findFacesCommand ?? (findFacesCommand = new RelayCommand(
                    p => isFindFacesButtonEnabled, p => FindFaces()));
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

        public FaceProcessor faceProcessor;
        public ObservableCollection<ImageInfo> ImageInfos { get; set; }
        public ObservableCollection<ImageInfo> GroupInfos { get; set; }
        public ObservableCollection<string> GroupNames { get; set; }

        public ImageProcessor()
        {
            GetDataFromIsolatedStorage();
            ImageInfos = new ObservableCollection<ImageInfo>();
            GroupInfos = new ObservableCollection<ImageInfo>();
            GroupNames = new ObservableCollection<string>();
            SetupVisionServices();
        }

        public void SetupVisionServices()
        {
            ((App)Application.Current).SetupComputerVisionClient(
                ComputerVisionKey, ComputerVisionEndpoint);
            ((App)Application.Current).SetupFaceClient(FaceKey, FaceEndpoint);

            computerVisionClient = ((App)Application.Current).computerVisionClient;
            faceClient = ((App)Application.Current).faceClient;

            faceProcessor = new FaceProcessor();
        }

        private async void FindFaces()
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

            // HIGH: without this statement, app suspends updating UI until explicit focus change (mouse or key event)
            await Task.Delay(1);
        }
        private void CancelFindFaces()
        {
            isCancelButtonEnabled = false;
            cancellationTokenSource.Cancel();
        }

        public async Task ProcessImageFilesForFacesAsync(
            FileInfo[] imageFiles, CancellationToken cancellationToken)
        {
            string thumbnailsFolder = imageFiles[0].DirectoryName +
                Path.DirectorySeparatorChar + thumbnailsFolderName;
            if (!Directory.Exists(thumbnailsFolder))
            {
                Directory.CreateDirectory(thumbnailsFolder);
            }

            ProcessingCount = 0;
            SearchedCount = 0;
            FaceCount = 0;
            FaceImageCount = 0;

            IList<DetectedFace> faceList;
            foreach (FileInfo file in imageFiles)
            {
                if (cancellationToken.IsCancellationRequested) { return; }

                ProcessingCount++;
                try
                {
                    using (FileStream stream = file.OpenRead())
                    {
                        faceList = await faceClient.Face.DetectWithStreamAsync(
                            stream, true, false,
                            new FaceAttributeType[]
                                { FaceAttributeType.Age, FaceAttributeType.Gender });
                    }
                    if (faceList.Count > 0)
                    {
                        SearchedCount++;

                        Guid detectedFaceId = Guid.Empty;

                        string attributes = string.Empty;
                        if (displayAttributes)
                        {
                            foreach (DetectedFace face in faceList)
                            {
                                detectedFaceId = (Guid)face.FaceId;
                                double? age = face.FaceAttributes.Age;
                                string gender = face.FaceAttributes.Gender.ToString();

                                if (searchAge && ( (age < MinAge) || (age > MaxAge) )) { continue; }
                                if (isMale && !gender.Equals(male)) { continue; }
                                if (isFemale && !gender.Equals(female)) { continue; }
                                attributes += gender + " " + age + "   ";
                            }
                            if (attributes.Equals(string.Empty)) { continue; }
                        }

                        ImageInfo newImage = new ImageInfo();
                        newImage.FilePath = file.DirectoryName + Path.DirectorySeparatorChar + file.Name;
                        newImage.FileName = file.Name;
                        newImage.Attributes = attributes;

                        if (getMetadata)
                        {
                            GetImageMetadata(file, newImage);
                        }

                        FaceImageCount++;

                        if (MatchFace && faceProcessor.IsPersonGroupTrained)
                        {
                            bool isFaceMatch = await faceProcessor.MatchFaceAsync(detectedFaceId);
                            if (!isFaceMatch) { continue; }
                        }

                        var tasks = new List<Task>();

                        if (getThumbnail)
                        {
                            Task thumbTask = ProcessImageFileForThumbAsync(file, newImage, thumbnailsFolder);
                            tasks.Add(thumbTask);
                        }
                        if (getCaption)
                        {
                            Task captionTask = ProcessImageFileForCaptionAsync(file, newImage);
                            tasks.Add(captionTask);
                        }
                        if (getOCR)
                        {
                            Task ocrTask = ProcessImageFileForTextAsync(file, newImage);
                            tasks.Add(ocrTask);
                        }

                        if (tasks.Count != 0)
                        {
                            await Task.WhenAll(tasks);
                        }

                        ImageInfos.Add(newImage);

                        if (MatchFace)
                        {
                            FaceCount = ImageInfos.Count;
                        }
                    }
                }
                // Catch and display Face errors.
                catch (APIErrorException fe)
                {
                    MessageBox.Show(fe.Message, "ProcessImageFilesForFacesAsync");
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "ProcessImageFilesForFacesAsync");
                }
            }
        }

        // Creates a thumbnail from newImage in the thumbnailsFolder.
        // Overwrites a file of the same name.
        private async Task<string> ProcessImageFileForThumbAsync(
            FileInfo file, ImageInfo newImage, string thumbnailsFolder)
        {
            if (!getThumbnail) { return string.Empty; }

            string thumbName = file.Name.Insert(file.Name.Length - 4, "_thumb");
            string thumbUrl = thumbnailsFolder + Path.DirectorySeparatorChar + thumbName;
            try
            {
                using (FileStream readStream = file.OpenRead(), writeStream = File.Create(thumbUrl))
                using (var thumbStream = await computerVisionClient.GenerateThumbnailInStreamAsync(
                            thumbWidth, thumbHeight, readStream, true))
                {
                    thumbStream.CopyTo(writeStream);
                }
                newImage.ThumbUrl = thumbUrl;
                return thumbUrl;
            }
            catch (ComputerVisionErrorException cve)
            {
                MessageBox.Show(cve.Message, "ProcessImageFileForThumbAsync");
                return string.Empty;
            }
        }

        private async Task<string> ProcessImageFileForCaptionAsync(
            FileInfo file, ImageInfo newImage)
        {
            if (!getCaption) { return string.Empty; }

            string caption = string.Empty;
            ImageDescription description;
            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    description = await computerVisionClient.DescribeImageInStreamAsync(stream);
                }
                if(description.Captions.Count > 0)
                {
                    caption = description.Captions[0].Text;
                }
                newImage.Caption = caption;
            }
            catch (ComputerVisionErrorException cve)
            {
                MessageBox.Show(cve.Message, "ProcessImageFileForCaptionAsync");
            }
            return caption;
        }

        private async Task<string> ProcessImageFileForTextAsync(
            FileInfo file, ImageInfo newImage)
        {
            if (!getOCR) { return string.Empty; }

            string ocrResult = string.Empty;
            OcrResult result;
            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    result = await computerVisionClient.RecognizePrintedTextInStreamAsync(true, stream);
                }
                IList<OcrRegion> regions = result.Regions;
                if (regions.Count > 0)
                {
                    foreach (OcrRegion region in regions)
                    {
                        foreach (OcrLine line in region.Lines)
                        {
                            foreach (OcrWord word in line.Words)
                            {
                                ocrResult += word.Text + " ";
                            }
                            break;
                        }
                        break;
                    }
                    newImage.OcrResult = ocrResult;
                }
            }
            catch (ComputerVisionErrorException cve)
            {
                MessageBox.Show(cve.Message, "ProcessImageFileForTextAsync");
            }
            return ocrResult;
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
                        dateTaken = bitmapMetadata.GetQuery(bitmapMetadata.DateTaken).ToString();
                    }
                    if (bitmapMetadata?.Title != null)
                    {
                        title = bitmapMetadata.GetQuery(bitmapMetadata.Title) as string;
                    }
                }
            }
            catch (NotSupportedException e) // The bitmap codec does not support the bitmap property.
            {
                //MessageBox.Show(e.Message, "GetImageMetadata: " + file.Name);
            }

            var metadata = dateTaken + " " + title;
            if (metadata.Equals(" ")) { metadata = string.Empty; }

            newImage.Metadata = metadata;
            return metadata;
        }

        private async void GetGroupNamesAsync()
        {
            GroupNames.Clear();
            IList<string> groupNames = await faceProcessor.GetAllPersonGroupNamesAsync();
            foreach (string name in groupNames)
            {
                GroupNames.Add(name);
            }
            //if (GroupNames.Contains(searchedForPerson))
            //{
            //    personName.SelectedItem = searchedForPerson;
            //}
        }

        private async Task AddToGroupAsync(object selectedThumbnails)
        {
            if (string.IsNullOrWhiteSpace(SearchedForPerson)) { return; }

            IList selectedItems = (IList)selectedThumbnails;
            IList<ImageInfo> items = selectedItems.Cast<ImageInfo>().ToList();
            await faceProcessor.AddFacesToPersonGroupAsync(SearchedForPerson, items, GroupInfos);
        }

        public void GetDataFromIsolatedStorage()
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

            SplashZIndex = 0;
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
